using System.Security.Cryptography;
using System.Text;
using DevPrep.Contracts;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using UserAPI.Models;
using UserAPI.Repositories;
using UserAPI.Repositories.Interfaces;
using UserAPI.Services;
using UserAPI.Services.Interfaces;

// Nạp biến môi trường từ backend/.env (RabbitMQ...). Không bắt buộc khi đã có env hệ thống.
try { Env.TraversePath().Load(".env.example"); } catch { }
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("USER_API_PORT", 7002);
// Chỉ lắng nghe trên máy cục bộ. Service này nằm sau ApiGateway, không có lý do gì để lộ ra
// toàn mạng LAN — bind 0.0.0.0 nghĩa là bất kỳ máy nào cùng mạng đều gọi thẳng được, bỏ qua
// mọi kiểm soát ở gateway. Vẫn cho phép override qua SERVICE_BIND_HOST để chạy trong container
// (Docker bắt buộc bind 0.0.0.0 thì mới publish port ra ngoài được).
var bindHost = builder.Configuration.GetValue<string>("SERVICE_BIND_HOST", "localhost");
builder.WebHost.UseUrls($"http://{bindHost}:{servicePort}");

// DbContext: CHỈ đọc từ .env (key USER_DB_CONNECTION_STRING, DotNetEnv nạp ở trên).
var connectionString = builder.Configuration.GetValue<string>("USER_DB_CONNECTION_STRING")
    ?? throw new InvalidOperationException("USER_DB_CONNECTION_STRING is missing in .env.");
builder.Services.AddDbContext<UserDbContext>(options => options.UseSqlServer(connectionString));

// Repositories + Services (kiến trúc phân lớp).
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

builder.Services.AddScoped<IAdminUserRepository, AdminUserRepository>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddHostedService<InactiveUserAutoBlockHostedService>();




// Đăng ký HttpClient gọi trực tiếp không thông qua file config/JSON
builder.Services.AddHttpClient("AuthServiceClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:7001/");
});

builder.Services.AddHttpClient("InterviewServiceClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:7005/");
});

// Khoá nội bộ, dùng cho hai chiều:
//  - GỬI ĐI: PaymentAPI chặn /api/internal/* bằng header này. Thiếu khoá thì lời gọi
//    last-activity nhận 401 và trang quản lý người dùng mất cột hoạt động gần nhất
//    (AdminUserService bắt lỗi best-effort nên sẽ hỏng âm thầm, không báo gì).
//  - NHẬN VỀ: chính UserAPI cũng dùng để bảo vệ /internal/* của mình (phía dưới).
// BẮT BUỘC phải có — fail-closed, thiếu khoá thì từ chối khởi động.
var internalApiKey = builder.Configuration.GetValue<string>("INTERNAL_API_KEY", string.Empty);
if (string.IsNullOrWhiteSpace(internalApiKey))
{
    throw new InvalidOperationException(
        "INTERNAL_API_KEY is missing. UserAPI refuses to start without it: " +
        "/internal/profiles/* (lookup, search, delete) would be reachable without authentication.");
}

builder.Services.AddHttpClient("PaymentServiceClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:7008/");
    if (!string.IsNullOrEmpty(internalApiKey))
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
});





// JWT: phải dùng cùng Issuer/Audience/Key với AuthAPI để verify được token do Auth phát ra.
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing in configuration.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "DevPrep.AuthAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "DevPrep.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddControllers();

// CORS cho WebClient (dev). Khi deploy nhớ thêm domain thật.
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClientDev", policy => policy
        .WithOrigins(
            "http://localhost:5130",
            "http://localhost:3000",
            "http://localhost:4200",
            "http://localhost:5500")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddEndpointsApiExplorer();
// Swagger có ô nhập Bearer token để test endpoint cần [Authorize].
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập access token (không cần chữ 'Bearer ')."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Phục vụ file tĩnh trong wwwroot (ảnh đại diện tại /avatars/...).
app.UseStaticFiles();

app.UseCors("WebClientDev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    service = "UserProfile",
    status = "ok"
}))
.WithName("GetHealth")
.WithOpenApi();

// ── Internal endpoint (thay cho consumer RabbitMQ) — AuthAPI gọi khi có tài khoản mới ──
// Idempotent: bỏ qua nếu hồ sơ đã tồn tại.
app.MapPost("/internal/profiles",
    async (AccountRegistered message, HttpRequest request, IUserProfileService service) =>
    {
        if (!IsInternalCallAuthorized(request, internalApiKey)) return Results.Unauthorized();
        await service.CreateProfileIfNotExistsAsync(message.AccountId, message.FullName);
        return Results.Ok();
    })
    .ExcludeFromDescription();

// Resolve FullName theo danh sách AccountId (InterviewAPI/admin gọi để hiển thị tên người dùng).
app.MapPost("/internal/profiles/lookup",
    async (UserAPI.DTOs.ProfileLookupRequest body, HttpRequest request, IUserProfileRepository repository) =>
    {
        if (!IsInternalCallAuthorized(request, internalApiKey)) return Results.Unauthorized();
        if (body.AccountIds is null || body.AccountIds.Count == 0)
            return Results.Ok(Array.Empty<UserAPI.DTOs.ProfileLookupItem>());

        var ids = body.AccountIds.Distinct().ToList();
        var profiles = await repository.GetByAccountIdsAsync(ids);
        var result = profiles
            .Select(p => new UserAPI.DTOs.ProfileLookupItem(p.AccountId, p.FullName))
            .ToList();
        return Results.Ok(result);
    })
    .ExcludeFromDescription();

// Tìm AccountId theo tên (FullName chứa keyword) — FeedbackAPI gọi để search feedback theo tên user.
app.MapPost("/internal/profiles/search-by-name",
    async (UserAPI.DTOs.ProfileSearchRequest body, HttpRequest request, IUserProfileRepository repository) =>
    {
        if (!IsInternalCallAuthorized(request, internalApiKey)) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(body.Keyword)) return Results.Ok(Array.Empty<Guid>());
        var ids = await repository.SearchAccountIdsByNameAsync(body.Keyword.Trim());
        return Results.Ok(ids);
    })
    .ExcludeFromDescription();

// Xoá hồ sơ theo AccountId — AuthAPI gọi khi dọn account Pending hết hạn xác thực email.
// Idempotent: không có hồ sơ tương ứng thì bỏ qua.
app.MapPost("/internal/profiles/delete",
    async (UserAPI.DTOs.ProfileDeleteRequest body, HttpRequest request, IUserProfileRepository repository) =>
    {
        if (!IsInternalCallAuthorized(request, internalApiKey)) return Results.Unauthorized();
        if (body.AccountIds is null || body.AccountIds.Count == 0) return Results.Ok();
        await repository.DeleteByAccountIdsAsync(body.AccountIds.Distinct().ToList());
        return Results.Ok();
    })
    .ExcludeFromDescription();

app.Run();

// So khớp header X-Internal-Api-Key với khoá cấu hình.
//
// Bản trước có nhánh "chưa cấu hình khoá -> cho qua" cho tiện chạy dev. Nhánh đó là bẫy: deploy
// mà quên set biến môi trường thì /internal/* mở toang và không có dấu hiệu gì báo. Ở service này
// hậu quả là ai cũng tra cứu được họ tên người dùng theo danh sách định danh, hoặc xoá hồ sơ.
// Nay khoá là bắt buộc (kiểm lúc khởi động).
static bool IsInternalCallAuthorized(HttpRequest request, string configuredKey)
{
    var provided = request.Headers["X-Internal-Api-Key"].ToString();
    // So sánh thời gian cố định: không rò rỉ độ dài khớp qua thời gian phản hồi.
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(provided),
        Encoding.UTF8.GetBytes(configuredKey));
}

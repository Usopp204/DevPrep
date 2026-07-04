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
builder.WebHost.UseUrls($"http://0.0.0.0:{servicePort}");

// DbContext: connection string lấy từ appsettings (ConnectionStrings:DefaultConnection),
// override được bằng ENV ConnectionStrings__DefaultConnection khi deploy.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");
builder.Services.AddDbContext<UserDbContext>(options => options.UseSqlServer(connectionString));

// Repositories + Services (kiến trúc phân lớp).
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

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

// Khoá nội bộ: chỉ service nào gửi kèm header X-Internal-Api-Key đúng mới gọi được /internal/*.
var internalApiKey = builder.Configuration.GetValue<string>("INTERNAL_API_KEY", string.Empty);

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

app.Run();

// So khớp header X-Internal-Api-Key với khoá cấu hình. Chưa cấu hình khoá -> cho qua (tiện chạy dev).
static bool IsInternalCallAuthorized(HttpRequest request, string? configuredKey)
{
    if (string.IsNullOrEmpty(configuredKey)) return true;
    return request.Headers.TryGetValue("X-Internal-Api-Key", out var provided)
        && string.Equals(provided.ToString(), configuredKey, StringComparison.Ordinal);
}

using AuthAPI.Middlewares;
using AuthAPI.Models;
using AuthAPI.Repositories;
using AuthAPI.Repositories.Interfaces;
using AuthAPI.Services;
using AuthAPI.Services.Interfaces;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

// Nạp biến môi trường từ file .env ở thư mục backend (RabbitMQ, SMTP...).
// Bọc try/catch để app vẫn chạy được nếu chưa có .env (khi đó dùng giá trị mặc định).
try { Env.TraversePath().Load(".env.example"); } catch { }
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

// Connection string đọc từ .env (key AUTH_DB_CONNECTION_STRING). DotNetEnv ở trên đã nạp key này
// vào biến môi trường nên builder.Configuration đọc được. KHÔNG hardcode trong appsettings/.cs.
var connectionString = builder.Configuration["AUTH_DB_CONNECTION_STRING"]
    ?? throw new InvalidOperationException("AUTH_DB_CONNECTION_STRING is missing in .env.");
builder.Services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(connectionString));

// Repositories
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
builder.Services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<ILoginAuditRepository, LoginAuditRepository>();
builder.Services.AddScoped<ISsoIdentityRepository, SsoIdentityRepository>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
// Job nền dọn account Pending hết hạn xác thực email (giải phóng email đăng ký lại).
builder.Services.AddHostedService<PendingAccountCleanupService>();
builder.Services.AddHttpClient<IGitHubOAuthClient, GitHubOAuthClient>();
// Verify Firebase ID token: gọi internet lấy khóa công khai của Google nên KHÔNG tắt proxy.
builder.Services.AddHttpClient<IFirebaseTokenValidator, FirebaseTokenValidator>();
builder.Services.AddHttpContextAccessor();

// Giao tiếp liên service qua HTTP (thay cho message bus RabbitMQ):
//   - NotificationAPI: gửi email OTP / đặt lại mật khẩu.
//   - UserAPI: tạo hồ sơ khi có tài khoản mới.
// BaseAddress + header khoá nội bộ (X-Internal-Api-Key) cấu hình một lần ở đây.
// Khoá là BẮT BUỘC: nó vừa dùng để gọi đi, vừa để chặn /internal/* khi nhận vào. Fail-closed —
// thiếu khoá thì service từ chối khởi động, thay vì chạy tiếp với /internal/* không được bảo vệ.
var internalApiKey = builder.Configuration.GetValue<string>("INTERNAL_API_KEY", string.Empty);
if (string.IsNullOrWhiteSpace(internalApiKey))
{
    throw new InvalidOperationException(
        "INTERNAL_API_KEY is missing. AuthAPI refuses to start without it: " +
        "/internal/* (account lookup, email search) would be reachable without authentication.");
}
var notificationBaseUrl = builder.Configuration.GetValue<string>("NOTIFICATION_API_BASEURL", "http://localhost:7010");
var userBaseUrl = builder.Configuration.GetValue<string>("USER_API_BASEURL", "http://localhost:7002");

// UseProxy=false: gọi nội bộ tới localhost, KHÔNG đi qua proxy hệ thống (proxy có thể chặn localhost).
builder.Services.AddHttpClient<INotificationApiClient, NotificationApiClient>(client =>
{
    client.BaseAddress = new Uri(notificationBaseUrl!);
    client.Timeout = TimeSpan.FromSeconds(15);
    if (!string.IsNullOrEmpty(internalApiKey))
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });

builder.Services.AddHttpClient<IUserApiClient, UserApiClient>(client =>
{
    client.BaseAddress = new Uri(userBaseUrl!);
    client.Timeout = TimeSpan.FromSeconds(15);
    if (!string.IsNullOrEmpty(internalApiKey))
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });

// JWT Authentication: xác thực access token cho các endpoint cần [Authorize] sau này.
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing in configuration.");

// AuthAPI là nơi DUY NHẤT ký token, nên chặn khoá yếu ở đây là chặn được tận gốc: không có token
// yếu nào ra đời thì các service khác không phải lo verify phải nó. Hai thứ bị từ chối:
//   - các giá trị mẫu từng nằm trong appsettings/.env.example (ai clone repo cũng biết);
//   - khoá ngắn hơn 32 ký tự — HS256 dùng khoá ngắn thì dò lại được bằng brute-force.
var weakJwtKeys = new[]
{
    "dev-only-change-me-please-use-a-long-random-secret-key-32+chars",
    "change-me-32-chars-min-random-secret-here"
};
if (weakJwtKeys.Contains(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is a placeholder or too short. AuthAPI refuses to start: tokens signed with a " +
        "publicly known key can be forged by anyone. Set a random Jwt__Key (32+ chars) in .env.");
}
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

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
// Swagger có ô nhập Bearer token để test các endpoint cần [Authorize] (vd đổi mật khẩu).
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhập access token (không cần chữ 'Bearer ')."
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS cho WebClient (môi trường dev). Khi deploy nhớ thêm domain thật vào danh sách.
// Cho phép các port FE phổ biến + trang test SSO. Không dùng AllowCredentials vì
// token đi qua body/Authorization header chứ không qua cookie.
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClientDev", policy => policy
        .WithOrigins(
            "http://localhost:5130",  // WebClient MVC
            "http://localhost:3000",  // CRA / Next.js
            "http://localhost:4200",  // Angular
            "http://localhost:5500",  // VS Code Live Server / trang sso-test
            "http://localhost:5173")  // Vite dev server (FrontendClient)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Bắt exception toàn cục, đặt sớm trong pipeline để bao trùm các middleware sau.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// CORS phải đứng trước Authentication/Authorization.
app.UseCors("WebClientDev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// AuthAPI trước đây là service duy nhất KHÔNG có /health, nên docker-compose không có cách nào
// biết nó đã sẵn sàng để cho các service phụ thuộc khởi động theo. Giữ đúng hình dạng phản hồi
// như 9 service kia để công cụ giám sát đọc thống nhất.
app.MapGet("/health", () => Results.Ok(new
{
    service = "Auth",
    status = "ok"
}))
.WithName("GetHealth")
.WithOpenApi();

// ── Internal endpoint: resolve email theo danh sách AccountId (InterviewAPI/admin gọi) ──
// Bảo vệ bằng header X-Internal-Api-Key (bắt buộc — xem IsInternalCallAuthorized ở cuối file).
app.MapPost("/internal/accounts/lookup",
    async (AuthAPI.DTOs.AccountLookupRequest body, HttpRequest request, IAccountRepository accounts) =>
    {
        if (!IsInternalCallAuthorized(request, internalApiKey)) return Results.Unauthorized();
        if (body.AccountIds is null || body.AccountIds.Count == 0)
            return Results.Ok(Array.Empty<AuthAPI.DTOs.AccountLookupItem>());

        var ids = body.AccountIds.Distinct().ToList();
        var list = await accounts.GetByIdsAsync(ids);
        var result = list.Select(a => new AuthAPI.DTOs.AccountLookupItem(a.AccountId, a.Email)).ToList();
        return Results.Ok(result);
    })
    .ExcludeFromDescription();

// Tìm AccountId theo email (Email chứa keyword) — FeedbackAPI gọi để search feedback theo email user.
app.MapPost("/internal/accounts/search-by-email",
    async (AuthAPI.DTOs.AccountSearchRequest body, HttpRequest request, IAccountRepository accounts) =>
    {
        if (!IsInternalCallAuthorized(request, internalApiKey)) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(body.Keyword)) return Results.Ok(Array.Empty<Guid>());
        var ids = await accounts.SearchAccountIdsByEmailAsync(body.Keyword.Trim());
        return Results.Ok(ids);
    })
    .ExcludeFromDescription();

app.Run();

// So khớp header X-Internal-Api-Key với khoá cấu hình.
//
// Bản trước có nhánh "chưa cấu hình khoá -> cho qua" cho tiện chạy dev. Nhánh đó là bẫy: deploy
// mà quên set biến môi trường thì toàn bộ /internal/* mở toang, và không có dấu hiệu gì báo —
// service vẫn khởi động bình thường, vẫn trả 200. Nay khoá là bắt buộc (kiểm lúc khởi động,
// xem chỗ đọc INTERNAL_API_KEY ở đầu file), nên ở đây chỉ còn một đường: khớp thì cho qua.
static bool IsInternalCallAuthorized(HttpRequest request, string configuredKey)
{
    var provided = request.Headers["X-Internal-Api-Key"].ToString();
    // So sánh thời gian cố định: không rò rỉ độ dài khớp qua thời gian phản hồi.
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(provided),
        Encoding.UTF8.GetBytes(configuredKey));
}

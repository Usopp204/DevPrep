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
using System.Text;

// Nạp biến môi trường từ file .env ở thư mục backend (RabbitMQ, SMTP...).
// Bọc try/catch để app vẫn chạy được nếu chưa có .env (khi đó dùng giá trị mặc định).
try { Env.TraversePath().Load(".env.example"); } catch { }
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

// Connection string lấy từ appsettings, có thể override bằng biến môi trường
// ConnectionStrings__DefaultConnection khi deploy lên server.
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
builder.Services.AddHttpClient<IGitHubOAuthClient, GitHubOAuthClient>();
builder.Services.AddHttpContextAccessor();

// Giao tiếp liên service qua HTTP (thay cho message bus RabbitMQ):
//   - NotificationAPI: gửi email OTP / đặt lại mật khẩu.
//   - UserAPI: tạo hồ sơ khi có tài khoản mới.
// BaseAddress + header khoá nội bộ (X-Internal-Api-Key) cấu hình một lần ở đây.
var internalApiKey = builder.Configuration.GetValue<string>("INTERNAL_API_KEY", string.Empty);
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

// CORS cho frontend (môi trường dev). Khi deploy nhớ thêm domain thật vào danh sách.
// Cho phép các port FE phổ biến + trang test SSO. Không dùng AllowCredentials vì
// token đi qua body/Authorization header chứ không qua cookie.
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy => policy
        .WithOrigins(
            "http://localhost:5173",  // Vite
            "http://localhost:3000",  // CRA / Next.js
            "http://localhost:4200",  // Angular
            "http://localhost:5500")  // VS Code Live Server / trang sso-test
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
app.UseCors("FrontendDev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ── Internal endpoint: resolve email theo danh sách AccountId (InterviewAPI/admin gọi) ──
// Bảo vệ bằng header X-Internal-Api-Key (khoá rỗng = bỏ qua kiểm tra, tiện chạy dev).
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

app.Run();

// So khớp header X-Internal-Api-Key với khoá cấu hình. Chưa cấu hình khoá -> cho qua (tiện chạy dev).
static bool IsInternalCallAuthorized(HttpRequest request, string? configuredKey)
{
    if (string.IsNullOrEmpty(configuredKey)) return true;
    return request.Headers.TryGetValue("X-Internal-Api-Key", out var provided)
        && string.Equals(provided.ToString(), configuredKey, StringComparison.Ordinal);
}

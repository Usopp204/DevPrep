using AuthAPI.Middlewares;
using AuthAPI.Models;
using AuthAPI.Repositories;
using AuthAPI.Repositories.Interfaces;
using AuthAPI.Services;
using AuthAPI.Services.Interfaces;
using DotNetEnv;
using MassTransit;
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

// MassTransit: AuthAPI chỉ publish event (EmailVerificationRequested) tới message bus.
var useRabbitMq = builder.Configuration.GetValue<bool>("USE_RABBITMQ", false);
var rabbitHost = builder.Configuration.GetValue<string>("GLOBAL_RABBITMQ_HOST", "localhost");
var rabbitUser = builder.Configuration.GetValue<string>("GLOBAL_RABBITMQ_USER", "guest");
var rabbitPass = builder.Configuration.GetValue<string>("GLOBAL_RABBITMQ_PASS", "guest");

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    if (useRabbitMq)
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbitHost, "/", h =>
            {
                h.Username(rabbitUser);
                h.Password(rabbitPass);
            });
            cfg.ConfigureEndpoints(context);
        });
    }
    else
    {
        // Fallback khi chưa bật RabbitMQ: transport in-memory (chỉ trong cùng tiến trình).
        x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
    }
});

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

app.Run();

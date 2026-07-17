using System.Security.Claims;
using System.Text;
using DotNetEnv;
using InterviewAPI.Clients;
using InterviewAPI.Models;
using InterviewAPI.Repositories;
using InterviewAPI.Repositories.Interfaces;
using InterviewAPI.Services;
using InterviewAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

try { Env.TraversePath().Load(".env.example"); } catch { }
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("INTERVIEW_API_PORT", 7005);
builder.WebHost.UseUrls($"http://0.0.0.0:{servicePort}");

// DbContext: connection string đọc từ .env (key INTERVIEW_DB_CONNECTION_STRING). DotNetEnv ở trên
// đã nạp key này vào biến môi trường nên builder.Configuration đọc được. KHÔNG hardcode trong appsettings/.cs.
var connectionString = builder.Configuration["INTERVIEW_DB_CONNECTION_STRING"]
    ?? throw new InvalidOperationException("INTERVIEW_DB_CONNECTION_STRING is missing in .env.");
builder.Services.AddDbContext<InterviewDbContext>(options => options.UseSqlServer(connectionString));

// Repositories + Services (kiến trúc phân lớp).
builder.Services.AddScoped<IInterviewRepository, InterviewRepository>();
builder.Services.AddScoped<IInterviewService, InterviewService>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IProfileBiometricsRepository, ProfileBiometricsRepository>();
builder.Services.AddScoped<IProfileBiometricsService, ProfileBiometricsService>();
builder.Services.AddScoped<ISessionVerificationChallengeRepository, SessionVerificationChallengeRepository>();
builder.Services.AddScoped<ISessionVerificationEventRepository, SessionVerificationEventRepository>();
builder.Services.AddScoped<IBiometricVerificationService, BiometricVerificationService>();

// Giao tiếp liên service qua HTTP (resolve danh tính người dùng của phiên phỏng vấn):
//   - AuthAPI: lấy Email theo AccountId.
//   - UserAPI: lấy FullName theo AccountId.
// UseProxy=false: gọi localhost không qua proxy hệ thống; kèm header khoá nội bộ X-Internal-Api-Key.
var internalApiKey = builder.Configuration.GetValue<string>("INTERNAL_API_KEY", string.Empty);
var authBaseUrl = builder.Configuration.GetValue<string>("AUTH_API_BASEURL", "http://localhost:7001");
var userBaseUrl = builder.Configuration.GetValue<string>("USER_API_BASEURL", "http://localhost:7002");
var aiServiceBaseUrl = builder.Configuration["AiService:BaseUrl"] ?? "http://localhost:8000";
var aiServiceApiKey = builder.Configuration["AiService:InternalApiKey"] ?? string.Empty;
var paymentBaseUrl = builder.Configuration.GetValue<string>("PAYMENT_API_BASEURL", "http://localhost:7008");
var evaluationBaseUrl = builder.Configuration.GetValue<string>("EVALUATION_API_BASEURL", "http://localhost:7006");

builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>(client =>
{
    client.BaseAddress = new Uri(authBaseUrl!);
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

builder.Services.AddHttpClient<IAiServiceClient, AiServiceClient>(client =>
{
    client.BaseAddress = new Uri(aiServiceBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
    if (!string.IsNullOrEmpty(aiServiceApiKey))
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", aiServiceApiKey);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });

// Calls PaymentAPI to deduct/refund credits (internal API key for interview credit system).
builder.Services.AddHttpClient<IPaymentApiClient, PaymentApiClient>(client =>
{
    client.BaseAddress = new Uri(paymentBaseUrl!);
    client.Timeout = TimeSpan.FromSeconds(30);
    if (!string.IsNullOrEmpty(internalApiKey))
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });

// Calls EvaluationAPI to grade each answer in the background (bearer token forwarded per call).
builder.Services.AddHttpClient<IEvaluationApiClient, EvaluationApiClient>(client =>
{
    client.BaseAddress = new Uri(evaluationBaseUrl!);
    client.Timeout = TimeSpan.FromSeconds(60);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });

// Needed to read the caller's bearer token so background grading can forward it to EvaluationAPI.
builder.Services.AddHttpContextAccessor();

// JWT: dùng chung Issuer/Audience/Key với AuthAPI để verify token Auth phát ra.
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
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClientDev", policy => policy
        .WithOrigins(
            "http://localhost:5130",
            "http://localhost:3000",
            "http://localhost:4200",
            "http://localhost:5500",
            "http://localhost:5173")   // Vite dev server (FrontendClient)
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

app.UseCors("WebClientDev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    service = "Interview",
    status = "ok"
}))
.WithName("GetHealth")
.WithOpenApi();

app.Run();

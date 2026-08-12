using System.Security.Claims;
using System.Text;
using DotNetEnv;
using EvaluationAPI.Clients;
using EvaluationAPI.Data;
using EvaluationAPI.Repositories;
using EvaluationAPI.Repositories.Interfaces;
using EvaluationAPI.Services;
using EvaluationAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Only the real .env: loading .env.example too let a missing key resolve to a placeholder.
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("EVALUATION_API_PORT", 7006);
// Chỉ lắng nghe trên máy cục bộ. Service này nằm sau ApiGateway, không có lý do gì để lộ ra
// toàn mạng LAN — bind 0.0.0.0 nghĩa là bất kỳ máy nào cùng mạng đều gọi thẳng được, bỏ qua
// mọi kiểm soát ở gateway. Vẫn cho phép override qua SERVICE_BIND_HOST để chạy trong container
// (Docker bắt buộc bind 0.0.0.0 thì mới publish port ra ngoài được).
var bindHost = builder.Configuration.GetValue<string>("SERVICE_BIND_HOST", "localhost");
builder.WebHost.UseUrls($"http://{bindHost}:{servicePort}");

// DbContext: CHỈ đọc từ .env (key EVALUATION_DB_CONNECTION_STRING, DotNetEnv nạp ở trên).
var connectionString = builder.Configuration.GetValue<string>("EVALUATION_DB_CONNECTION_STRING")
    ?? throw new InvalidOperationException("EVALUATION_DB_CONNECTION_STRING is missing in .env.");

builder.Services.AddDbContext<EvaluationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Repositories + Services (kiến trúc phân lớp).
builder.Services.AddScoped<IEvaluationRepository, EvaluationRepository>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();

// Gọi ai-service để chấm điểm + tổng hợp báo cáo. UseProxy=false: gọi localhost không qua proxy.
// No literal fallback: the old default was http://localhost:8000, but this service's own
// appsettings.json says 8080 (Docker publishes ai-service as 8080:8000). A missing key used
// to fail later as an unreachable ai-service; now it fails at startup, naming the real cause.
var aiServiceBaseUrl = builder.Configuration["AiService:BaseUrl"]
    ?? throw new InvalidOperationException("AiService:BaseUrl is not configured.");
var aiServiceApiKey = builder.Configuration["AiService:InternalApiKey"] ?? string.Empty;

builder.Services.AddHttpClient<IAiServiceClient, AiServiceClient>(client =>
{
    client.BaseAddress = new Uri(aiServiceBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
    if (!string.IsNullOrEmpty(aiServiceApiKey))
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", aiServiceApiKey);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });

// Verifies session ownership against InterviewAPI (Evaluation tables have no UserId of their own),
// forwarding the caller's bearer token. Guards every action against IDOR.
var interviewBaseUrl = builder.Configuration.GetValue<string>("INTERVIEW_API_BASEURL", "http://localhost:7005");
builder.Services.AddHttpClient<IInterviewApiClient, InterviewApiClient>(client =>
{
    client.BaseAddress = new Uri(interviewBaseUrl!);
    client.Timeout = TimeSpan.FromSeconds(15);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });

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
    service = "Evaluation",
    status = "ok"
}))
.WithName("GetHealth")
.WithOpenApi();

app.Run();

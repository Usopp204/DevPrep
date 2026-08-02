using System.Security.Claims;
using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StatisticsAPI.Clients;
using StatisticsAPI.Data;
using StatisticsAPI.Services;

try { Env.TraversePath().Load(".env.example"); } catch { }
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("STATISTICS_API_PORT", 7011);
builder.WebHost.UseUrls($"http://0.0.0.0:{servicePort}");

// ── DB: chỉ đọc từ .env (STATISTICS_DB_CONNECTION_STRING) ─────────────────────
var connectionString = builder.Configuration.GetValue<string>("STATISTICS_DB_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("STATISTICS_DB_CONNECTION_STRING is missing in .env.");
builder.Services.AddDbContext<StatisticsDbContext>(options => options.UseSqlServer(connectionString));

// ── JWT: verify token do AuthAPI phát (cùng Issuer/Audience/Key) ──────────────
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing.");
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
builder.Services.AddAuthorization();

// ── Gọi các service nguồn QUA ApiGateway (thống nhất một cửa; UseProxy=false vì
// proxy hệ thống chặn localhost). Override bằng GATEWAY_BASEURL khi deploy.
var gatewayBaseUrl = builder.Configuration.GetValue<string>("GATEWAY_BASEURL", "http://localhost:7000")!;

builder.Services.AddHttpClient("gateway", c =>
    {
        c.BaseAddress = new Uri(gatewayBaseUrl);
        c.Timeout = TimeSpan.FromSeconds(20);
    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });

// ── DI ────────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IServiceTokenFactory, ServiceTokenFactory>();
builder.Services.AddSingleton<StatisticsCollectionGate>();
builder.Services.AddScoped<ISourceMetricsClient, SourceMetricsClient>();
builder.Services.AddScoped<IStatisticsQueryService, StatisticsQueryService>();
builder.Services.AddScoped<IStatisticsCollector, StatisticsCollector>();
builder.Services.AddHostedService<StatisticsRollupHostedService>();

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClientDev", policy => policy
        .WithOrigins("http://localhost:5130", "http://localhost:3000", "http://localhost:4200", "http://localhost:5500", "http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddEndpointsApiExplorer();
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
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
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

app.MapGet("/health", () => Results.Ok(new { service = "Statistics", status = "ok" }))
    .WithName("GetHealth")
    .WithOpenApi();

app.Run();

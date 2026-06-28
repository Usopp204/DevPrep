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

// DbContext: connection string lấy từ appsettings (ConnectionStrings:DefaultConnection có Encrypt=False
// để tránh treo ~14s ở post-login phase trên SQL Server local). Override bằng ENV khi deploy.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");
builder.Services.AddDbContext<InterviewDbContext>(options => options.UseSqlServer(connectionString));

// Repositories + Services (kiến trúc phân lớp).
builder.Services.AddScoped<IInterviewRepository, InterviewRepository>();
builder.Services.AddScoped<IInterviewService, InterviewService>();

// Giao tiếp liên service qua HTTP (resolve danh tính người dùng của phiên phỏng vấn):
//   - AuthAPI: lấy Email theo AccountId.
//   - UserAPI: lấy FullName theo AccountId.
// UseProxy=false: gọi localhost không qua proxy hệ thống; kèm header khoá nội bộ X-Internal-Api-Key.
var internalApiKey = builder.Configuration.GetValue<string>("INTERNAL_API_KEY", string.Empty);
var authBaseUrl = builder.Configuration.GetValue<string>("AUTH_API_BASEURL", "http://localhost:7001");
var userBaseUrl = builder.Configuration.GetValue<string>("USER_API_BASEURL", "http://localhost:7002");

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

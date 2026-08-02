using System.Security.Claims;
using System.Text;
using DotNetEnv;
using FeedbackAPI.DTOs;
using FeedbackAPI.Models;
using FeedbackAPI.Repositories;
using FeedbackAPI.Repositories.Interfaces;
using FeedbackAPI.Services;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

try { Env.TraversePath().Load(".env.example"); } catch { }
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("FEEDBACK_API_PORT", 7007);
builder.WebHost.UseUrls($"http://0.0.0.0:{servicePort}");

// DbContext: CHỈ đọc từ .env (key FEEDBACK_DB_CONNECTION_STRING, DotNetEnv nạp ở trên).
var connectionString = builder.Configuration.GetValue<string>("FEEDBACK_DB_CONNECTION_STRING")
    ?? throw new InvalidOperationException("FEEDBACK_DB_CONNECTION_STRING is missing in .env.");
builder.Services.AddDbContext<FeedbackDbContext>(options => options.UseSqlServer(connectionString));

// Repositories + Services (kiến trúc phân lớp). Tách riêng AI feedback và System feedback.
builder.Services.AddScoped<IAiFeedbackRepository, AiFeedbackRepository>();
builder.Services.AddScoped<ISystemFeedbackRepository, SystemFeedbackRepository>();
builder.Services.AddScoped<IFeedbackReportRepository, FeedbackReportRepository>();
builder.Services.AddScoped<IAiFeedbackService, AiFeedbackService>();
builder.Services.AddScoped<ISystemFeedbackService, SystemFeedbackService>();
builder.Services.AddScoped<IFeedbackReportService, FeedbackReportService>();
builder.Services.AddScoped<IFeedbackStatisticsService, FeedbackStatisticsService>();

// Client nội bộ resolve Tên (UserAPI) + Email (AuthAPI) cho AI feedback. UseProxy=false + header khoá nội bộ.
var internalApiKey = builder.Configuration.GetValue<string>("INTERNAL_API_KEY", string.Empty);
var userBaseUrl = builder.Configuration.GetValue<string>("USER_API_BASEURL", "http://localhost:7002");
var authBaseUrl = builder.Configuration.GetValue<string>("AUTH_API_BASEURL", "http://localhost:7001");
builder.Services.AddHttpClient<FeedbackAPI.Clients.IUserApiClient, FeedbackAPI.Clients.UserApiClient>(client =>
{
    client.BaseAddress = new Uri(userBaseUrl!);
    client.Timeout = TimeSpan.FromSeconds(15);
    if (!string.IsNullOrEmpty(internalApiKey))
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });
builder.Services.AddHttpClient<FeedbackAPI.Clients.IAuthApiClient, FeedbackAPI.Clients.AuthApiClient>(client =>
{
    client.BaseAddress = new Uri(authBaseUrl!);
    client.Timeout = TimeSpan.FromSeconds(15);
    if (!string.IsNullOrEmpty(internalApiKey))
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });

var interviewBaseUrl = builder.Configuration.GetValue<string>("INTERVIEW_API_BASEURL", "http://localhost:7005");
builder.Services.AddHttpClient<FeedbackAPI.Clients.IInterviewApiClient, FeedbackAPI.Clients.InterviewApiClient>(client =>
{
    client.BaseAddress = new Uri(interviewBaseUrl!);
    client.Timeout = TimeSpan.FromSeconds(15);
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

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var message = context.ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(error => !string.IsNullOrWhiteSpace(error))
                ?? "The request is invalid.";

            return new BadRequestObjectResult(ApiResponse<object>.Fail(message));
        };
    });

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
    service = "Feedback",
    status = "ok"
}))
.WithName("GetHealth")
.WithOpenApi();

app.Run();

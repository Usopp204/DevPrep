using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestionAPI.Data;
using QuestionAPI.Repositories.Interfaces;
using QuestionAPI.Repositories;
using QuestionAPI.Services;
using QuestionAPI.Services.Interfaces;

try { Env.TraversePath().Load(".env.example"); } catch { }
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("QUESTION_API_PORT", 7004);
builder.WebHost.UseUrls($"http://0.0.0.0:{servicePort}");

// ── Connection String: CHỈ đọc từ .env (key QUESTION_DB_CONNECTION_STRING) ───
var connectionString =
    builder.Configuration.GetValue<string>("QUESTION_DB_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("QUESTION_DB_CONNECTION_STRING is missing in .env.");

builder.Services.AddDbContext<QuestionBankDbContext>(options =>
    options.UseSqlServer(connectionString));

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey    = builder.Configuration["Jwt:Key"]      ?? throw new InvalidOperationException("Jwt:Key is missing.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]   ?? "DevPrep.AuthAPI";
var jwtAud    = builder.Configuration["Jwt:Audience"] ?? "DevPrep.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAud,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
    });

builder.Services.AddAuthorization();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IQuestionService, QuestionService>();

builder.Services.AddScoped<IAdminQuestionRepository, AdminQuestionRepository>();
builder.Services.AddScoped<IAdminQuestionService, AdminQuestionService>();
builder.Services.AddScoped<InternalQuestionService>();
builder.Services.AddScoped<ITechStackRepository, TechStackRepository>();
builder.Services.AddScoped<ITechStackService, TechStackService>();
builder.Services.AddScoped<IQuestionIndexEligibilityService, QuestionIndexEligibilityService>();
builder.Services.AddScoped<IPositionRepository, PositionRepository>();
builder.Services.AddScoped<IPositionService, PositionService>();

// CORS cho trang admin WebClient gọi trực tiếp (cùng policy các service khác).
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


//------ SỬA LẠI ĐOẠN NÀY -------
// Calls ai-service to keep the ChromaDB question index in sync with admin CRUD.
// Same convention as InterviewAPI: AiService:BaseUrl (host access = :8080) + X-Internal-Api-Key.
var aiServiceBaseUrl = builder.Configuration["AiService:BaseUrl"] ?? "http://localhost:8080";
var aiServiceApiKey = builder.Configuration["AiService:InternalApiKey"] ?? string.Empty;

builder.Services.AddHttpClient<IQuestionIndexClient, QuestionIndexClient>(client =>
{
    client.BaseAddress = new Uri(aiServiceBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
    if (!string.IsNullOrEmpty(aiServiceApiKey))
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", aiServiceApiKey);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });

// ── Controllers & Swagger ─────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "QuestionAPI", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter JWT token",
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

// ── Build ─────────────────────────────────────────────────────────────────────
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

app.MapGet("/health", () => Results.Ok(new { service = "QuestionBank", status = "ok" }))
   .WithName("GetHealth")
   .WithOpenApi();

app.Run();



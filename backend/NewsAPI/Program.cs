using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using NewsAPI.Models;
using NewsAPI.Repositories;
using NewsAPI.Repositories.Interfaces;
using NewsAPI.Services;
using NewsAPI.Services.Interfaces;

try { Env.TraversePath().Load(".env.example"); } catch { }
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("NEWS_API_PORT", 7009);
builder.WebHost.UseUrls($"http://0.0.0.0:{servicePort}");

// DbContext: connection string lấy từ appsettings (ConnectionStrings:DefaultConnection).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");
builder.Services.AddDbContext<NewsDbContext>(options => options.UseSqlServer(connectionString));

// Repositories + Services.
builder.Services.AddScoped<INewsRepository, NewsRepository>();
builder.Services.AddScoped<INewsService, NewsService>();

builder.Services.AddControllers();

// CORS cho frontend (dev). Tin tức là công khai nên không cần xác thực.
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy => policy
        .WithOrigins(
            "http://localhost:5173",
            "http://localhost:3000",
            "http://localhost:4200",
            "http://localhost:5500")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FrontendDev");

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    service = "News",
    status = "ok"
}))
.WithName("GetHealth")
.WithOpenApi();

app.Run();

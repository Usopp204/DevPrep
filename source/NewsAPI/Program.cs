using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NewsAPI.Models;
using NewsAPI.Repositories;
using NewsAPI.Repositories.Interfaces;
using NewsAPI.Services;
using NewsAPI.Services.Interfaces;

try { Env.TraversePath().Load(".env.example"); } catch { }
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("NEWS_API_PORT", 7009);
// Chỉ lắng nghe trên máy cục bộ. Service này nằm sau ApiGateway, không có lý do gì để lộ ra
// toàn mạng LAN — bind 0.0.0.0 nghĩa là bất kỳ máy nào cùng mạng đều gọi thẳng được, bỏ qua
// mọi kiểm soát ở gateway. Vẫn cho phép override qua SERVICE_BIND_HOST để chạy trong container
// (Docker bắt buộc bind 0.0.0.0 thì mới publish port ra ngoài được).
var bindHost = builder.Configuration.GetValue<string>("SERVICE_BIND_HOST", "localhost");
builder.WebHost.UseUrls($"http://{bindHost}:{servicePort}");

// CHỈ đọc từ .env (key NEWS_DB_CONNECTION_STRING, DotNetEnv nạp ở trên).
var connectionString = builder.Configuration.GetValue<string>("NEWS_DB_CONNECTION_STRING")
    ?? throw new InvalidOperationException("NEWS_DB_CONNECTION_STRING is missing in .env.");
builder.Services.AddDbContext<NewsDbContext>(options => options.UseSqlServer(connectionString));

// Không đặt khoá mặc định ở đây. Giá trị mặc định ngầm nguy hiểm hơn là thiếu cấu hình: service
// vẫn khởi động, vẫn nhận token, nhưng lại verify bằng một khoá ai đọc mã nguồn cũng biết.
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing in configuration (set Jwt__Key in .env).");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "DevPrep.AuthAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "DevPrep.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<INewsRepository, NewsRepository>();
builder.Services.AddScoped<INewsService, NewsService>();
builder.Services.AddScoped<IAdminNewsRepository, AdminNewsRepository>();
builder.Services.AddScoped<IAdminNewsService, AdminNewsService>();

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
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "NewsAPI", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token as: Bearer {token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
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
    service = "News",
    status = "ok"
}))
.WithName("GetHealth")
.WithOpenApi();

app.Run();

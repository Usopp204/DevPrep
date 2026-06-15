using System.Text;
using DotNetEnv;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using UserAPI.Consumers;
using UserAPI.Models;
using UserAPI.Repositories;
using UserAPI.Repositories.Interfaces;
using UserAPI.Services;
using UserAPI.Services.Interfaces;

// Nạp biến môi trường từ backend/.env (RabbitMQ...). Không bắt buộc khi đã có env hệ thống.
try { Env.TraversePath().Load(); } catch { /* .env không bắt buộc */ }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("USER_API_PORT", 7002);
builder.WebHost.UseUrls($"http://0.0.0.0:{servicePort}");

// DbContext: connection string lấy từ appsettings (ConnectionStrings:DefaultConnection),
// override được bằng ENV ConnectionStrings__DefaultConnection khi deploy.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");
builder.Services.AddDbContext<UserDbContext>(options => options.UseSqlServer(connectionString));

// Repositories + Services (kiến trúc phân lớp).
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

// JWT: phải dùng cùng Issuer/Audience/Key với AuthAPI để verify được token do Auth phát ra.
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

builder.Services.AddControllers();

// CORS cho frontend (dev). Khi deploy nhớ thêm domain thật.
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

var rabbitHost = builder.Configuration.GetValue<string>("GLOBAL_RABBITMQ_HOST", "localhost");
var rabbitUser = builder.Configuration.GetValue<string>("GLOBAL_RABBITMQ_USER", "guest");
var rabbitPass = builder.Configuration.GetValue<string>("GLOBAL_RABBITMQ_PASS", "guest");
var useRabbitMq = builder.Configuration.GetValue<bool>("USE_RABBITMQ", false);

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    // Consumer tạo hồ sơ khi có tài khoản mới (event AccountRegistered từ AuthAPI).
    x.AddConsumer<AccountRegisteredConsumer>();

    if (useRabbitMq)
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbitHost, "/", h =>
            {
                h.Username(rabbitUser);
                h.Password(rabbitPass);
            });

            cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
            cfg.ConfigureEndpoints(context);
        });
    }
    else
    {
        x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Phục vụ file tĩnh trong wwwroot (ảnh đại diện tại /avatars/...).
app.UseStaticFiles();

app.UseCors("FrontendDev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    service = "UserProfile",
    status = "ok"
}))
.WithName("GetHealth")
.WithOpenApi();

app.Run();

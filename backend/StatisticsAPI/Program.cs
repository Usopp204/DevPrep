using DotNetEnv;
using StatisticsAPI.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

try { Env.TraversePath().Load(".env.example"); } catch { }
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("STATISTICS_API_PORT", 7011);
builder.WebHost.UseUrls($"http://0.0.0.0:{servicePort}");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetValue<string>("STATISTICS_DB_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("STATISTICS_DB_CONNECTION_STRING is missing.");
}

builder.Services.AddDbContext<StatisticsDbContext>(options =>
    options.UseSqlServer(connectionString));

var rabbitHost = builder.Configuration.GetValue<string>("GLOBAL_RABBITMQ_HOST", "localhost");
var rabbitUser = builder.Configuration.GetValue<string>("GLOBAL_RABBITMQ_USER", "guest");
var rabbitPass = builder.Configuration.GetValue<string>("GLOBAL_RABBITMQ_PASS", "guest");
var useRabbitMq = builder.Configuration.GetValue<bool>("USE_RABBITMQ", false);

if (string.IsNullOrWhiteSpace(rabbitHost) || string.IsNullOrWhiteSpace(rabbitUser) || string.IsNullOrWhiteSpace(rabbitPass))
{
    throw new InvalidOperationException("RabbitMQ config is missing or invalid.");
}

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

            cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
            cfg.ConfigureEndpoints(context);
        });
    }
    else
    {
        x.UsingInMemory((context, cfg) =>
        {
            cfg.ConfigureEndpoints(context);
        });
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new
{
    service = "Statistics",
    status = "ok"
}))
.WithName("GetHealth")
.WithOpenApi();

app.Run();


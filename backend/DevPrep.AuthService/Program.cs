using DotNetEnv;
using DevPrep.AuthService.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Service listen port (AUTH_)
var authPort = builder.Configuration.GetValue<int>("AUTH_API_PORT", 7001);
builder.WebHost.UseUrls($"http://0.0.0.0:{authPort}");

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetValue<string>("AUTH_DB_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("AUTH_DB_CONNECTION_STRING is missing.");
}

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(connectionString));

// MassTransit + RabbitMQ
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

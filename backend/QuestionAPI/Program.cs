using DotNetEnv;
using QuestionAPI.Data;
using Microsoft.EntityFrameworkCore;

try { Env.TraversePath().Load(".env.example"); } catch { }
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("QUESTION_API_PORT", 7004);
builder.WebHost.UseUrls($"http://0.0.0.0:{servicePort}");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetValue<string>("QUESTION_DB_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("QUESTION_DB_CONNECTION_STRING is missing.");
}

builder.Services.AddDbContext<QuestionBankDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new
{
    service = "QuestionBank",
    status = "ok"
}))
.WithName("GetHealth")
.WithOpenApi();

app.Run();


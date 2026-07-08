using DotNetEnv;
using CvJdAPI.Data;
using CvJdAPI.Endpoints;
using Microsoft.EntityFrameworkCore;

try { Env.TraversePath().Load(".env.example"); } catch { }
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("DOC_API_PORT", 7003);
builder.WebHost.UseUrls($"http://0.0.0.0:{servicePort}");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("ai", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

var connectionString = builder.Configuration.GetValue<string>("DOC_DB_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("DOC_DB_CONNECTION_STRING is missing.");
}

builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

// Always return JSON for unhandled exceptions
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    ctx.Response.ContentType = "application/json";
    ctx.Response.StatusCode = 500;
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    await ctx.Response.WriteAsJsonAsync(new { error = ex?.Message ?? "Internal server error" });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new
{
    service = "Document",
    status = "ok"
}))
.WithName("GetHealth")
.WithOpenApi();

app.MapCvEndpoints();
app.MapJdEndpoints();
app.MapMatchEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
    db.Database.EnsureCreated();
}

app.Run();


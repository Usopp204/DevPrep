using DotNetEnv;
using Microsoft.AspNetCore.Http.Timeouts;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Gateway listen port (GLOBAL_)
var gatewayPort = builder.Configuration.GetValue<int>("GLOBAL_API_GATEWAY_PORT", 7000);
builder.WebHost.UseUrls($"http://0.0.0.0:{gatewayPort}");

// Body size limit (10MB)
var bodyLimitMb = builder.Configuration.GetValue<int>("Gateway:BodySizeLimitMb", 10);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = bodyLimitMb * 1024 * 1024;
});

// Global timeout (30s)
var timeoutSeconds = builder.Configuration.GetValue<int>("Gateway:RequestTimeoutSeconds", 30);
builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(timeoutSeconds)
    };
});

// CORS (Frontend -> Gateway)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseRequestTimeouts();
app.UseCors();
app.MapReverseProxy();

app.Run();

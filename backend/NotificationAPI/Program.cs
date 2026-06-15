using DotNetEnv;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NotificationAPI.Consumers;
using NotificationAPI.Models;
using NotificationAPI.Repositories;
using NotificationAPI.Repositories.Interfaces;
using NotificationAPI.Services;
using NotificationAPI.Services.Interfaces;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("NOTIFICATION_API_PORT", 7010);
builder.WebHost.UseUrls($"http://0.0.0.0:{servicePort}");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetValue<string>("NOTIFICATION_DB_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("NOTIFICATION_DB_CONNECTION_STRING is missing.");
}

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Repositories
builder.Services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>();
builder.Services.AddScoped<IEmailLogRepository, EmailLogRepository>();

// Services
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<INotificationService, NotificationService>();

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

    // Consumer lắng nghe event EmailVerificationRequested + PasswordResetRequested từ AuthAPI.
    x.AddConsumer<EmailVerificationRequestedConsumer>();
    x.AddConsumer<PasswordResetRequestedConsumer>();

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

// Seed template VERIFY_EMAIL nếu chưa có (chạy 1 lần lúc khởi động).
await SeedEmailTemplatesAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new
{
    service = "Notification",
    status = "ok"
}))
.WithName("GetHealth")
.WithOpenApi();

app.Run();

static async Task SeedEmailTemplatesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var templates = scope.ServiceProvider.GetRequiredService<IEmailTemplateRepository>();

    if (!await templates.ExistsByCodeAsync("VERIFY_EMAIL"))
    {
        await templates.AddAsync(new EmailTemplate
        {
            Code = "VERIFY_EMAIL",
            Subject = "Xác thực email DevPrep của bạn",
            BodyHtml = """
                <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto">
                  <h2>DevPrep – Xác thực email</h2>
                  <p>Mã OTP xác thực tài khoản của bạn là:</p>
                  <p style="font-size:28px;font-weight:bold;letter-spacing:4px">{{otp}}</p>
                  <p>Mã có hiệu lực trong <b>{{minutes}} phút</b>. Vui lòng không chia sẻ mã này cho bất kỳ ai.</p>
                  <hr/>
                  <p style="color:#888;font-size:12px">Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>
                </div>
                """,
            IsActive = true
        });
        await templates.SaveChangesAsync();
    }

    if (!await templates.ExistsByCodeAsync("PASSWORD_RESET"))
    {
        await templates.AddAsync(new EmailTemplate
        {
            Code = "PASSWORD_RESET",
            Subject = "Đặt lại mật khẩu DevPrep",
            BodyHtml = """
                <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto">
                  <h2>DevPrep – Đặt lại mật khẩu</h2>
                  <p>Bạn (hoặc ai đó) vừa yêu cầu đặt lại mật khẩu cho tài khoản <b>{{email}}</b>.</p>
                  <p>Nhấn vào nút bên dưới để đặt mật khẩu mới:</p>
                  <p style="margin:24px 0">
                    <a href="{{resetUrl}}" style="background:#1f883d;color:#fff;text-decoration:none;padding:12px 20px;border-radius:6px;font-weight:bold">Đặt lại mật khẩu</a>
                  </p>
                  <p style="font-size:12px;color:#555">Hoặc mở liên kết: <br/>{{resetUrl}}</p>
                  <p>Liên kết có hiệu lực trong <b>{{minutes}} phút</b>.</p>
                  <hr/>
                  <p style="color:#888;font-size:12px">Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này — mật khẩu của bạn vẫn an toàn.</p>
                </div>
                """,
            IsActive = true
        });
        await templates.SaveChangesAsync();
    }
}

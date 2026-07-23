using DevPrep.Contracts;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using NotificationAPI.Models;
using NotificationAPI.Repositories;
using NotificationAPI.Repositories.Interfaces;
using NotificationAPI.Services;
using NotificationAPI.Services.Interfaces;

try { Env.TraversePath().Load(".env.example"); } catch { }
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("NOTIFICATION_API_PORT", 7010);
builder.WebHost.UseUrls($"http://0.0.0.0:{servicePort}");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CHỈ đọc từ .env (key NOTIFICATION_DB_CONNECTION_STRING, DotNetEnv nạp ở trên).
var connectionString = builder.Configuration.GetValue<string>("NOTIFICATION_DB_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("NOTIFICATION_DB_CONNECTION_STRING is missing in .env.");
}

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 5)));

// Repositories
builder.Services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>();
builder.Services.AddScoped<IEmailLogRepository, EmailLogRepository>();

// Services
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Khoá nội bộ: chỉ service nào gửi kèm header X-Internal-Api-Key đúng mới gọi được /internal/*.
var internalApiKey = builder.Configuration.GetValue<string>("INTERNAL_API_KEY", string.Empty);

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

// ── Internal endpoints (thay cho consumer RabbitMQ) — AuthAPI gọi qua HTTP ──
app.MapPost("/internal/notifications/email-verification",
    async (EmailVerificationRequested message, HttpRequest request, INotificationService service, CancellationToken ct) =>
    {
        if (!IsInternalCallAuthorized(request, internalApiKey)) return Results.Unauthorized();
        await service.SendEmailVerificationAsync(message, ct);
        return Results.Ok();
    })
    .ExcludeFromDescription();

app.MapPost("/internal/notifications/password-reset",
    async (PasswordResetRequested message, HttpRequest request, INotificationService service, CancellationToken ct) =>
    {
        if (!IsInternalCallAuthorized(request, internalApiKey)) return Results.Unauthorized();
        await service.SendPasswordResetAsync(message, ct);
        return Results.Ok();
    })
    .ExcludeFromDescription();

app.Run();

// So khớp header X-Internal-Api-Key với khoá cấu hình. Chưa cấu hình khoá -> cho qua (tiện chạy dev).
static bool IsInternalCallAuthorized(HttpRequest request, string? configuredKey)
{
    if (string.IsNullOrEmpty(configuredKey)) return true;
    return request.Headers.TryGetValue("X-Internal-Api-Key", out var provided)
        && string.Equals(provided.ToString(), configuredKey, StringComparison.Ordinal);
}

static async Task SeedEmailTemplatesAsync(WebApplication app)
{
    // Nội dung mặc định của VERIFY_EMAIL (OTP + nút link xác thực) — giữ đồng bộ với
    // DefaultBodyHtml trong NotificationService.
    const string verifyEmailBodyHtml = """
        <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto">
          <h2>DevPrep – Xác thực email</h2>
          <p>Mã OTP xác thực tài khoản của bạn là:</p>
          <p style="font-size:28px;font-weight:bold;letter-spacing:4px">{{otp}}</p>
          <p>Hoặc nhấn nút bên dưới để xác thực trực tiếp, không cần nhập mã:</p>
          <p style="margin:24px 0">
            <a href="{{verifyUrl}}" style="background:#1f883d;color:#fff;text-decoration:none;padding:12px 20px;border-radius:6px;font-weight:bold">Xác thực email</a>
          </p>
          <p style="font-size:12px;color:#555">Hoặc mở liên kết: <br/>{{verifyUrl}}</p>
          <p>Mã và liên kết có hiệu lực trong <b>{{minutes}} phút</b>. Vui lòng không chia sẻ cho bất kỳ ai.</p>
          <hr/>
          <p style="color:#888;font-size:12px">Nếu bạn không đăng ký tài khoản DevPrep, hãy bỏ qua email này — tài khoản chưa xác thực sẽ tự động bị xoá sau khi mã hết hạn.</p>
        </div>
        """;

    using var scope = app.Services.CreateScope();
    var templates = scope.ServiceProvider.GetRequiredService<IEmailTemplateRepository>();

    // VERIFY_EMAIL: tạo mới nếu chưa có; nếu DB còn bản cũ (chưa có link xác thực)
    // thì nâng cấp body để email chứa cả OTP lẫn nút link.
    var verifyTemplate = await templates.GetByCodeAsync("VERIFY_EMAIL");
    if (verifyTemplate is null)
    {
        await templates.AddAsync(new EmailTemplate
        {
            Code = "VERIFY_EMAIL",
            Subject = "Xác thực email DevPrep của bạn",
            BodyHtml = verifyEmailBodyHtml,
            IsActive = true
        });
        await templates.SaveChangesAsync();
    }
    else if (!verifyTemplate.BodyHtml.Contains("{{verifyUrl}}"))
    {
        verifyTemplate.BodyHtml = verifyEmailBodyHtml;
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

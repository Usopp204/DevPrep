using DevPrep.Contracts;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NotificationAPI.Models;
using NotificationAPI.Repositories;
using NotificationAPI.Repositories.Interfaces;
using NotificationAPI.Services;
using NotificationAPI.Services.Interfaces;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

// Only the real .env: loading .env.example too let a missing key resolve to a placeholder.
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("NOTIFICATION_API_PORT", 7010);
// Chỉ lắng nghe trên máy cục bộ. Service này nằm sau ApiGateway, không có lý do gì để lộ ra
// toàn mạng LAN — bind 0.0.0.0 nghĩa là bất kỳ máy nào cùng mạng đều gọi thẳng được, bỏ qua
// mọi kiểm soát ở gateway. Vẫn cho phép override qua SERVICE_BIND_HOST để chạy trong container
// (Docker bắt buộc bind 0.0.0.0 thì mới publish port ra ngoài được).
var bindHost = builder.Configuration.GetValue<string>("SERVICE_BIND_HOST", "localhost");
builder.WebHost.UseUrls($"http://{bindHost}:{servicePort}");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing.");
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
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AnnouncementReader", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("AnnouncementAdmin", policy => policy.RequireRole("Admin"));
});

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
builder.Services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();

// Services
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Khoá nội bộ: chỉ service nào gửi kèm header X-Internal-Api-Key đúng mới gọi được /internal/*.
// BẮT BUỘC phải có — fail-closed, thiếu khoá thì từ chối khởi động thay vì để endpoint gửi email
// mở cho bất kỳ ai gọi.
var internalApiKey = builder.Configuration.GetValue<string>("INTERNAL_API_KEY", string.Empty);
if (string.IsNullOrWhiteSpace(internalApiKey))
{
    throw new InvalidOperationException(
        "INTERNAL_API_KEY is missing. NotificationAPI refuses to start without it: " +
        "/internal/notifications/* (send email) would be reachable without authentication.");
}

var app = builder.Build();

// Seed template VERIFY_EMAIL nếu chưa có (chạy 1 lần lúc khởi động).
await SeedEmailTemplatesAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    service = "Notification",
    status = "ok"
}))
.WithName("GetHealth")
.WithOpenApi();

// ── Announcements (thông báo hệ thống hiển thị popup trên homepage) ──────────
// PUBLIC: guest + user đều đọc được (WebClient gọi server-side qua gateway
// /api/notifications/announcements — route strip prefix nên tới đây là /announcements).
app.MapGet("/announcements", async (IAnnouncementRepository repo) =>
{
    var items = await repo.GetActiveAsync(5);
    return Results.Ok(items.Select(a => new
    {
        a.AnnouncementId,
        a.Title,
        a.Content,
        a.CreatedAt
    }));
});

// NotificationAPI enforces the service-level authorization boundary; WebClient role checks
// provide UI-level protection only.
app.MapGet("/admin/announcements", async (IAnnouncementRepository repo, bool onlyDeleted = false) =>
{
    var items = await repo.GetAdminListAsync(onlyDeleted, 100);
    return Results.Ok(items.Select(a => new
    {
        a.AnnouncementId, a.Title, a.Content, a.IsActive, a.IsDeleted, a.DeletedAt, a.CreatedAt, a.UpdatedAt
    }));
})
.RequireAuthorization("AnnouncementReader");

app.MapPost("/admin/announcements", async (AnnouncementRequest req, IAnnouncementRepository repo) =>
{
    var error = ValidateAnnouncement(req);
    if (error is not null) return Results.BadRequest(new { Message = error });

    var now = NotificationAPI.Helpers.VietnamTime.Now;
    var announcement = new Announcement
    {
        AnnouncementId = Guid.NewGuid(),
        Title = req.Title.Trim(),
        Content = req.Content.Trim(),
        IsActive = req.IsActive,
        IsDeleted = false,
        CreatedAt = now,
        UpdatedAt = now
    };
    await repo.AddAsync(announcement);
    await repo.SaveChangesAsync();
    return Results.Ok(new { announcement.AnnouncementId });
})
.RequireAuthorization("AnnouncementAdmin");

app.MapPut("/admin/announcements/{id:guid}", async (Guid id, AnnouncementRequest req, IAnnouncementRepository repo) =>
{
    var error = ValidateAnnouncement(req);
    if (error is not null) return Results.BadRequest(new { Message = error });

    var announcement = await repo.GetTrackedByIdAsync(id);
    if (announcement is null || announcement.IsDeleted)
        return Results.NotFound(new { Message = "Announcement not found." });

    announcement.Title = req.Title.Trim();
    announcement.Content = req.Content.Trim();
    announcement.IsActive = req.IsActive;
    announcement.UpdatedAt = NotificationAPI.Helpers.VietnamTime.Now;
    await repo.SaveChangesAsync();
    return Results.Ok(new { announcement.AnnouncementId });
})
.RequireAuthorization("AnnouncementAdmin");

app.MapDelete("/admin/announcements/{id:guid}", async (Guid id, IAnnouncementRepository repo) =>
{
    var announcement = await repo.GetTrackedByIdAsync(id);
    if (announcement is null) return Results.NotFound(new { Message = "Announcement not found." });
    if (!announcement.IsDeleted)
    {
        var now = NotificationAPI.Helpers.VietnamTime.Now;
        announcement.IsDeleted = true;
        announcement.DeletedAt = now;
        announcement.UpdatedAt = now;
        await repo.SaveChangesAsync();
    }
    return Results.NoContent();
})
.RequireAuthorization("AnnouncementAdmin");

app.MapPost("/admin/announcements/{id:guid}/restore", async (Guid id, IAnnouncementRepository repo) =>
{
    var announcement = await repo.GetTrackedByIdAsync(id);
    if (announcement is null) return Results.NotFound(new { Message = "Announcement not found." });
    if (announcement.IsDeleted)
    {
        announcement.IsDeleted = false;
        announcement.DeletedAt = null;
        announcement.UpdatedAt = NotificationAPI.Helpers.VietnamTime.Now;
        await repo.SaveChangesAsync();
    }
    return Results.Ok(new { announcement.AnnouncementId });
})
.RequireAuthorization("AnnouncementAdmin");

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

// Validate chung cho create/update announcement. Trả message lỗi hoặc null nếu hợp lệ.
static string? ValidateAnnouncement(AnnouncementRequest req)
{
    if (string.IsNullOrWhiteSpace(req.Title)) return "Title is required.";
    if (req.Title.Trim().Length > 200) return "Title must not exceed 200 characters.";
    if (string.IsNullOrWhiteSpace(req.Content)) return "Content is required.";
    if (req.Content.Trim().Length > 2000) return "Content must not exceed 2000 characters.";
    return null;
}

// So khớp header X-Internal-Api-Key với khoá cấu hình.
//
// Bản trước có nhánh "chưa cấu hình khoá -> cho qua" cho tiện chạy dev. Nhánh đó là bẫy: deploy
// mà quên set biến môi trường thì /internal/* mở toang và không có dấu hiệu gì báo. Ở service này
// hậu quả là ai cũng bắt hệ thống gửi email tuỳ ý. Nay khoá là bắt buộc (kiểm lúc khởi động).
static bool IsInternalCallAuthorized(HttpRequest request, string configuredKey)
{
    var provided = request.Headers["X-Internal-Api-Key"].ToString();
    // So sánh thời gian cố định: không rò rỉ độ dài khớp qua thời gian phản hồi.
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(provided),
        Encoding.UTF8.GetBytes(configuredKey));
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

/// <summary>Body tạo/sửa announcement từ trang admin.</summary>
record AnnouncementRequest(string Title, string Content, bool IsActive = true);

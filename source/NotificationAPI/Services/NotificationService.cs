using DevPrep.Contracts;
using NotificationAPI.Helpers;
using NotificationAPI.Models;
using NotificationAPI.Repositories.Interfaces;
using NotificationAPI.Services.Interfaces;

namespace NotificationAPI.Services;

public class NotificationService : INotificationService
{
    private const string VerifyEmailTemplateCode = "VERIFY_EMAIL";
    private const string PasswordResetTemplateCode = "PASSWORD_RESET";
    private const string StatusSent = "Sent";
    private const string StatusFailed = "Failed";

    private readonly IEmailTemplateRepository _templateRepository;
    private readonly IEmailLogRepository _emailLogRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IEmailTemplateRepository templateRepository,
        IEmailLogRepository emailLogRepository,
        IEmailSender emailSender,
        ILogger<NotificationService> logger)
    {
        _templateRepository = templateRepository;
        _emailLogRepository = emailLogRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task SendEmailVerificationAsync(EmailVerificationRequested message, CancellationToken cancellationToken = default)
    {
        var minutes = Math.Max(1, (int)Math.Round((message.ExpiresAt - VietnamTime.Now).TotalMinutes));

        // Lấy template trong DB; nếu chưa có thì dùng nội dung mặc định.
        var template = await _templateRepository.GetActiveByCodeAsync(VerifyEmailTemplateCode);
        var subject = template?.Subject ?? "Xác thực email DevPrep của bạn";
        var bodyTemplate = template?.BodyHtml ?? DefaultBodyHtml;

        var body = bodyTemplate
            .Replace("{{otp}}", message.Otp)
            .Replace("{{email}}", message.Email)
            .Replace("{{minutes}}", minutes.ToString());

        var log = new EmailLog
        {
            RecipientEmail = message.Email,
            UserId = message.AccountId,
            TemplateCode = VerifyEmailTemplateCode,
            Subject = subject,
            Body = body
        };

        try
        {
            await _emailSender.SendAsync(message.Email, subject, body, cancellationToken);
            log.Status = StatusSent;
            log.SentAt = VietnamTime.Now;
            _logger.LogInformation("Sent verification email to {Email}.", message.Email);
        }
        catch (Exception ex)
        {
            log.Status = StatusFailed;
            log.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to send verification email to {Email}.", message.Email);
        }

        await _emailLogRepository.AddAsync(log);
        await _emailLogRepository.SaveChangesAsync();
    }

    public async Task SendPasswordResetAsync(PasswordResetRequested message, CancellationToken cancellationToken = default)
    {
        var minutes = Math.Max(1, (int)Math.Round((message.ExpiresAt - VietnamTime.Now).TotalMinutes));

        var template = await _templateRepository.GetActiveByCodeAsync(PasswordResetTemplateCode);
        var subject = template?.Subject ?? "Đặt lại mật khẩu DevPrep";
        var bodyTemplate = template?.BodyHtml ?? DefaultPasswordResetBodyHtml;

        var body = bodyTemplate
            .Replace("{{resetUrl}}", message.ResetUrl)
            .Replace("{{email}}", message.Email)
            .Replace("{{minutes}}", minutes.ToString());

        var log = new EmailLog
        {
            RecipientEmail = message.Email,
            UserId = message.AccountId,
            TemplateCode = PasswordResetTemplateCode,
            Subject = subject,
            Body = body
        };

        try
        {
            await _emailSender.SendAsync(message.Email, subject, body, cancellationToken);
            log.Status = StatusSent;
            log.SentAt = VietnamTime.Now;
            _logger.LogInformation("Sent password reset email to {Email}.", message.Email);
        }
        catch (Exception ex)
        {
            log.Status = StatusFailed;
            log.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to send password reset email to {Email}.", message.Email);
        }

        await _emailLogRepository.AddAsync(log);
        await _emailLogRepository.SaveChangesAsync();
    }

    private const string DefaultBodyHtml = """
        <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto">
          <h2>DevPrep – Xác thực email</h2>
          <p>Mã OTP xác thực tài khoản của bạn là:</p>
          <p style="font-size:28px;font-weight:bold;letter-spacing:4px">{{otp}}</p>
          <p>Mã có hiệu lực trong <b>{{minutes}} phút</b>. Vui lòng không chia sẻ mã này cho bất kỳ ai.</p>
          <hr/>
          <p style="color:#888;font-size:12px">Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>
        </div>
        """;

    private const string DefaultPasswordResetBodyHtml = """
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
        """;
}

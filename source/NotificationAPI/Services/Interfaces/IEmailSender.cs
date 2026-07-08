namespace NotificationAPI.Services.Interfaces;

/// <summary>
/// Gửi email cấp thấp. Tách interface để dễ thay nhà cung cấp (SMTP, SendGrid...) sau này.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

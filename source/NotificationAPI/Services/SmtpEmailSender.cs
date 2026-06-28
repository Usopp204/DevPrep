using System.Net;
using System.Net.Mail;
using NotificationAPI.Services.Interfaces;

namespace NotificationAPI.Services;

/// <summary>
/// Gửi email qua SMTP (mặc định cấu hình cho Gmail). Đọc thông số từ biến môi trường (.env).
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _pass;
    private readonly string _fromAddress;
    private readonly string _fromName;

    public SmtpEmailSender(IConfiguration configuration)
    {
        _host = configuration.GetValue<string>("SMTP_HOST") ?? "smtp.gmail.com";
        _port = configuration.GetValue<int?>("SMTP_PORT") ?? 587;
        _user = configuration.GetValue<string>("SMTP_USER") ?? string.Empty;

        // App Password của Gmail thường được copy kèm khoảng trắng -> bỏ hết space.
        _pass = (configuration.GetValue<string>("SMTP_PASS") ?? string.Empty).Replace(" ", string.Empty);

        // From phải là email hợp lệ; nếu cấu hình sai thì dùng tài khoản SMTP đã xác thực.
        var from = configuration.GetValue<string>("SMTP_FROM");
        _fromAddress = !string.IsNullOrWhiteSpace(from) && from.Contains('@') ? from : _user;

        _fromName = configuration.GetValue<string>("SMTP_FROM_NAME") ?? "DevPrep";
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_host, _port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_user, _pass)
        };

        using var message = new MailMessage
        {
            From = new MailAddress(_fromAddress, _fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        await client.SendMailAsync(message, cancellationToken);
    }
}

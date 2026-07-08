using DevPrep.Contracts;

namespace AuthAPI.Services.Interfaces;

/// <summary>
/// Gọi NotificationAPI qua HTTP (thay cho message bus RabbitMQ) để gửi email.
/// Best-effort: nơi gọi bọc try/catch nên lỗi gửi không chặn luồng chính (đăng ký / quên mật khẩu).
/// </summary>
public interface INotificationApiClient
{
    /// <summary>Yêu cầu NotificationAPI gửi email chứa OTP xác thực.</summary>
    Task SendEmailVerificationAsync(EmailVerificationRequested message, CancellationToken cancellationToken = default);

    /// <summary>Yêu cầu NotificationAPI gửi email chứa liên kết đặt lại mật khẩu.</summary>
    Task SendPasswordResetAsync(PasswordResetRequested message, CancellationToken cancellationToken = default);
}

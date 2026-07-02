using DevPrep.Contracts;

namespace NotificationAPI.Services.Interfaces;

/// <summary>
/// Xử lý nghiệp vụ gửi thông báo/email. Consumer chỉ gọi vào đây, không chứa logic.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Gửi email chứa OTP xác thực và ghi log vào EmailLogs.
    /// </summary>
    Task SendEmailVerificationAsync(EmailVerificationRequested message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi email chứa liên kết đặt lại mật khẩu và ghi log vào EmailLogs.
    /// </summary>
    Task SendPasswordResetAsync(PasswordResetRequested message, CancellationToken cancellationToken = default);
}

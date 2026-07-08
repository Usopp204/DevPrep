namespace DevPrep.Contracts;

/// <summary>
/// Sự kiện AuthAPI phát ra khi cần gửi email chứa OTP xác thực (đăng ký hoặc gửi lại OTP).
/// NotificationAPI lắng nghe sự kiện này để gửi email và ghi log.
/// </summary>
public record EmailVerificationRequested
{
    public Guid AccountId { get; init; }

    public string Email { get; init; } = null!;

    public string Otp { get; init; } = null!;

    public DateTime ExpiresAt { get; init; }
}

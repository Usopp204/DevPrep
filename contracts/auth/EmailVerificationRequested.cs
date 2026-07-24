namespace DevPrep.Contracts;

/// <summary>
/// Sự kiện AuthAPI phát ra khi cần gửi email xác thực (đăng ký hoặc gửi lại OTP).
/// NotificationAPI lắng nghe sự kiện này để gửi email và ghi log.
/// </summary>
public record EmailVerificationRequested
{
    public Guid AccountId { get; init; }

    public string Email { get; init; } = null!;

    public string Otp { get; init; } = null!;

    /// <summary>
    /// Link xác thực đầy đủ (đã kèm token) trỏ về trang ConfirmEmail của WebClient —
    /// người dùng bấm nút trong email thay vì nhập OTP. Null với caller cũ chưa gửi link.
    /// </summary>
    public string? VerifyUrl { get; init; }

    public DateTime ExpiresAt { get; init; }
}

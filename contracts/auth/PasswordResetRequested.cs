namespace DevPrep.Contracts;

/// <summary>
/// Sự kiện AuthAPI phát ra khi người dùng yêu cầu đặt lại mật khẩu (quên mật khẩu).
/// NotificationAPI lắng nghe để gửi email chứa liên kết đặt lại mật khẩu và ghi log.
/// </summary>
public record PasswordResetRequested
{
    public Guid AccountId { get; init; }

    public string Email { get; init; } = null!;

    /// <summary>Liên kết đặt lại mật khẩu đầy đủ (đã kèm token) trỏ về trang WebClient.</summary>
    public string ResetUrl { get; init; } = null!;

    public DateTime ExpiresAt { get; init; }
}

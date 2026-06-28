namespace AuthAPI.DTOs;

/// <summary>
/// Kết quả trả về sau khi gửi lại OTP thành công.
/// </summary>
public class ResendEmailOtpResponse
{
    public string Email { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
}

namespace AuthAPI.DTOs;

/// <summary>
/// Kết quả trả về sau khi xác thực email thành công.
/// </summary>
public class VerifyEmailOtpResponse
{
    public Guid AccountId { get; set; }

    public string Email { get; set; } = null!;

    public bool IsEmailVerified { get; set; }

    public string Status { get; set; } = null!;
}

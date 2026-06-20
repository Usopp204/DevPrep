namespace AuthAPI.DTOs;

/// <summary>
/// Kết quả trả về sau khi đặt lại mật khẩu thành công.
/// </summary>
public class ResetPasswordResponse
{
    public string Email { get; set; } = null!;

    public string Message { get; set; } = null!;
}

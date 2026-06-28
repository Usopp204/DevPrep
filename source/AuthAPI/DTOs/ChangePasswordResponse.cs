namespace AuthAPI.DTOs;

/// <summary>
/// Kết quả trả về sau khi đổi mật khẩu thành công.
/// </summary>
public class ChangePasswordResponse
{
    public string Email { get; set; } = null!;

    public string Message { get; set; } = null!;
}

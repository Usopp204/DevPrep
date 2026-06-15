namespace AuthAPI.DTOs;

/// <summary>
/// Kết quả trả về cho yêu cầu quên mật khẩu. Thông báo cố tình chung chung để
/// không tiết lộ email nào đã đăng ký (chống dò email - account enumeration).
/// </summary>
public class ForgotPasswordResponse
{
    public string Message { get; set; } = null!;
}

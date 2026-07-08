using System.ComponentModel.DataAnnotations;

namespace AuthAPI.DTOs;

/// <summary>
/// Dữ liệu client gửi lên khi yêu cầu đặt lại mật khẩu (quên mật khẩu).
/// </summary>
public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is not a valid email address.")]
    [StringLength(256, ErrorMessage = "Email must not exceed 256 characters.")]
    public string Email { get; set; } = null!;
}

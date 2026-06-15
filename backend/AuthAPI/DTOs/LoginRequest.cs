using System.ComponentModel.DataAnnotations;

namespace AuthAPI.DTOs;

/// <summary>
/// Dữ liệu client gửi lên khi đăng nhập (dùng chung cho cả user và admin).
/// </summary>
public class LoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is not a valid email address.")]
    [StringLength(256, ErrorMessage = "Email must not exceed 256 characters.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = null!;
}

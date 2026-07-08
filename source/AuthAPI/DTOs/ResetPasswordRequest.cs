using System.ComponentModel.DataAnnotations;

namespace AuthAPI.DTOs;

/// <summary>
/// Dữ liệu client gửi lên để đặt lại mật khẩu bằng token nhận từ email.
/// </summary>
public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Reset token is required.")]
    public string Token { get; set; } = null!;

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters.")]
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter and one digit.")]
    public string NewPassword { get; set; } = null!;

    [Required(ErrorMessage = "Confirm password is required.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Confirm password does not match new password.")]
    public string ConfirmPassword { get; set; } = null!;
}

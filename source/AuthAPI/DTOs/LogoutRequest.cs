using System.ComponentModel.DataAnnotations;

namespace AuthAPI.DTOs;

/// <summary>
/// Dữ liệu client gửi lên để đăng xuất: thu hồi refresh token hiện tại.
/// </summary>
public class LogoutRequest
{
    [Required(ErrorMessage = "Refresh token is required.")]
    public string RefreshToken { get; set; } = null!;
}

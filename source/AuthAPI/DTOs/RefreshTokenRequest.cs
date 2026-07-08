using System.ComponentModel.DataAnnotations;

namespace AuthAPI.DTOs;

/// <summary>
/// Dữ liệu client gửi lên để xin cấp access token mới bằng refresh token.
/// </summary>
public class RefreshTokenRequest
{
    [Required(ErrorMessage = "Refresh token is required.")]
    public string RefreshToken { get; set; } = null!;
}

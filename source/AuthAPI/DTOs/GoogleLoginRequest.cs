using System.ComponentModel.DataAnnotations;

namespace AuthAPI.DTOs;

/// <summary>
/// Đăng nhập bằng Google: WebClient gửi lên ID token nhận được từ Google Sign-In.
/// </summary>
public class GoogleLoginRequest
{
    [Required(ErrorMessage = "Google ID token is required.")]
    public string IdToken { get; set; } = null!;
}

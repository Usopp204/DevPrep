using System.ComponentModel.DataAnnotations;

namespace AuthAPI.DTOs;

/// <summary>
/// Đăng nhập bằng GitHub: WebClient gửi lên authorization code nhận được sau khi
/// người dùng đồng ý trên GitHub. Backend sẽ đổi code này lấy thông tin người dùng.
/// </summary>
public class GitHubLoginRequest
{
    [Required(ErrorMessage = "GitHub authorization code is required.")]
    public string Code { get; set; } = null!;

    /// <summary>
    /// Redirect URI đã dùng ở bước authorize (nếu có) — phải khớp khi đổi code.
    /// </summary>
    public string? RedirectUri { get; set; }
}

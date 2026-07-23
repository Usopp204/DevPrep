using System.ComponentModel.DataAnnotations;

namespace WebClient.Models.Auth;

/// <summary>
/// Dữ liệu trang xác thực email qua link (người dùng bấm nút trong email).
/// Token lấy từ query string của link, gửi lên backend khi bấm nút xác nhận.
/// </summary>
public class ConfirmEmailViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;
}

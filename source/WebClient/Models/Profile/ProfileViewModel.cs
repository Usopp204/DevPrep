namespace WebClient.Models.Profile;

/// <summary>
/// Dữ liệu hiển thị trang Profile (xem). Lấy từ GET /api/users/me qua gateway.
/// </summary>
public class ProfileViewModel
{
    public string Email { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? AvatarUrl { get; set; }

    /// <summary>Tên hiển thị: FullName nếu có, ngược lại lấy phần trước @@ của email.</summary>
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(FullName)
            ? FullName!
            : (Email.Contains('@') ? Email[..Email.IndexOf('@')] : Email);

    /// <summary>Chữ cái đầu để hiện avatar khi chưa có ảnh.</summary>
    public string Initial =>
        string.IsNullOrWhiteSpace(DisplayName) ? "U" : DisplayName.Trim()[..1].ToUpperInvariant();

    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);
}

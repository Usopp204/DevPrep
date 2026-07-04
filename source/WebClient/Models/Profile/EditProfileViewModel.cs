using System.ComponentModel.DataAnnotations;

namespace WebClient.Models.Profile;

/// <summary>
/// Dữ liệu form Edit Profile. Gửi lên PUT /api/users/me dạng multipart/form-data
/// (FullName bắt buộc; Avatar tuỳ chọn — chỉ gửi khi đổi ảnh).
/// </summary>
public class EditProfileViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 150 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Email chỉ để hiển thị (readonly) — không cho sửa.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Ảnh đại diện hiện tại (để preview); không phải field gửi lên.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Ảnh mới chọn từ máy. Bỏ trống nếu chỉ đổi tên.</summary>
    public IFormFile? Avatar { get; set; }

    public string Initial =>
        string.IsNullOrWhiteSpace(FullName)
            ? (Email.Length > 0 ? Email[..1].ToUpperInvariant() : "U")
            : FullName.Trim()[..1].ToUpperInvariant();

    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);
}

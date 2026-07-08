using System.ComponentModel.DataAnnotations;

namespace UserAPI.DTOs;

/// <summary>
/// Dữ liệu cập nhật hồ sơ (gửi dạng multipart/form-data).
/// FullName bắt buộc; Avatar tuỳ chọn — chỉ gửi khi muốn đổi ảnh đại diện.
/// </summary>
public class UpdateProfileRequest
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 150 characters.")]
    public string FullName { get; set; } = null!;

    /// <summary>Ảnh đại diện mới (chọn từ máy). Bỏ trống nếu chỉ đổi tên.</summary>
    public IFormFile? Avatar { get; set; }
}

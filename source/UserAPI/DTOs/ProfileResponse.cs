namespace UserAPI.DTOs;

/// <summary>
/// Thông tin hồ sơ trả về cho client: Email (lấy từ JWT/Auth) + FullName + Avatar
/// (lấy từ bảng UserProfiles). FullName/AvatarUrl có thể null nếu chưa cập nhật hồ sơ.
/// </summary>
public class ProfileResponse
{
    public string Email { get; set; } = null!;

    public string? FullName { get; set; }

    public string? AvatarUrl { get; set; }
}

using UserAPI.DTOs;

namespace UserAPI.Services.Interfaces;

public interface IUserProfileService
{
    /// <summary>
    /// Lấy hồ sơ của chính người dùng đang đăng nhập. Email lấy từ JWT (truyền vào),
    /// FullName/Avatar lấy từ bảng UserProfiles theo accountId.
    /// </summary>
    Task<ProfileResponse> GetMyProfileAsync(Guid accountId, string email);

    /// <summary>
    /// Tạo hồ sơ cho một tài khoản mới (gọi khi nhận event AccountRegistered).
    /// Idempotent: nếu hồ sơ đã tồn tại thì bỏ qua (phòng event bị gửi lại).
    /// </summary>
    Task CreateProfileIfNotExistsAsync(Guid accountId, string? fullName);

    /// <summary>
    /// Cập nhật hồ sơ của chính người dùng: đổi FullName và (tuỳ chọn) ảnh đại diện.
    /// Upsert: nếu chưa có hồ sơ thì tạo mới.
    /// </summary>
    Task<ProfileResponse> UpdateMyProfileAsync(Guid accountId, string email, string fullName, IFormFile? avatar, CancellationToken cancellationToken = default);
}

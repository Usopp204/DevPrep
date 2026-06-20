namespace UserAPI.Services.Interfaces;

/// <summary>
/// Lưu trữ file (ảnh đại diện...). Tách interface để sau này dễ đổi sang cloud
/// (Azure Blob, S3, Cloudinary...) mà không sửa logic nghiệp vụ.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Lưu ảnh đại diện và trả về URL công khai để hiển thị.</summary>
    Task<string> SaveAvatarAsync(IFormFile file, CancellationToken cancellationToken = default);

    /// <summary>Xoá ảnh đại diện cũ (best-effort) nếu nó do service này lưu.</summary>
    void DeleteAvatar(string? avatarUrl);
}

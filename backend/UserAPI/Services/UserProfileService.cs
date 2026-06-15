using UserAPI.DTOs;
using UserAPI.Helpers;
using UserAPI.Models;
using UserAPI.Repositories.Interfaces;
using UserAPI.Services.Interfaces;

namespace UserAPI.Services;

public class UserProfileService : IUserProfileService
{
    private readonly IUserProfileRepository _profileRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(
        IUserProfileRepository profileRepository,
        IFileStorageService fileStorage,
        ILogger<UserProfileService> logger)
    {
        _profileRepository = profileRepository;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<ProfileResponse> GetMyProfileAsync(Guid accountId, string email)
    {
        // FullName/Avatar có thể chưa có (hồ sơ chưa được tạo/cập nhật) -> trả null.
        var profile = await _profileRepository.GetByAccountIdAsync(accountId);

        return new ProfileResponse
        {
            Email = email,
            FullName = profile?.FullName,
            AvatarUrl = profile?.AvatarUrl
        };
    }

    public async Task CreateProfileIfNotExistsAsync(Guid accountId, string? fullName)
    {
        // Idempotent: event có thể được gửi lại -> chỉ tạo khi chưa có hồ sơ.
        if (await _profileRepository.ExistsByAccountIdAsync(accountId))
        {
            _logger.LogInformation("Hồ sơ cho account {AccountId} đã tồn tại, bỏ qua.", accountId);
            return;
        }

        var now = VietnamTime.Now;
        await _profileRepository.AddAsync(new UserProfile
        {
            AccountId = accountId,
            FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        });
        await _profileRepository.SaveChangesAsync();

        _logger.LogInformation("Đã tạo hồ sơ cho account {AccountId}.", accountId);
    }

    public async Task<ProfileResponse> UpdateMyProfileAsync(
        Guid accountId, string email, string fullName, IFormFile? avatar, CancellationToken cancellationToken = default)
    {
        var profile = await _profileRepository.GetTrackedByAccountIdAsync(accountId);
        var now = VietnamTime.Now;
        var isNew = profile is null;

        // Upsert: chưa có hồ sơ (account cũ hoặc lúc đăng ký broker tắt) -> tạo mới.
        profile ??= new UserProfile { AccountId = accountId, CreatedAt = now };

        profile.FullName = fullName.Trim();

        if (avatar is not null)
        {
            var newUrl = await _fileStorage.SaveAvatarAsync(avatar, cancellationToken);
            _fileStorage.DeleteAvatar(profile.AvatarUrl); // xoá ảnh cũ (best-effort)
            profile.AvatarUrl = newUrl;
        }

        profile.UpdatedAt = now;

        if (isNew)
        {
            await _profileRepository.AddAsync(profile);
        }
        await _profileRepository.SaveChangesAsync();

        _logger.LogInformation("Đã cập nhật hồ sơ cho account {AccountId} (avatar đổi: {Changed}).",
            accountId, avatar is not null);

        return new ProfileResponse
        {
            Email = email,
            FullName = profile.FullName,
            AvatarUrl = profile.AvatarUrl
        };
    }
}

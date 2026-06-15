using UserAPI.Models;

namespace UserAPI.Repositories.Interfaces;

public interface IUserProfileRepository
{
    /// <summary>Lấy hồ sơ theo AccountId (tham chiếu logic tới Auth). Null nếu chưa có hồ sơ.</summary>
    Task<UserProfile?> GetByAccountIdAsync(Guid accountId);

    /// <summary>Lấy hồ sơ có theo dõi thay đổi (tracking) để cập nhật.</summary>
    Task<UserProfile?> GetTrackedByAccountIdAsync(Guid accountId);

    /// <summary>Kiểm tra đã có hồ sơ cho AccountId hay chưa (để tạo idempotent).</summary>
    Task<bool> ExistsByAccountIdAsync(Guid accountId);

    Task AddAsync(UserProfile profile);

    Task<int> SaveChangesAsync();
}

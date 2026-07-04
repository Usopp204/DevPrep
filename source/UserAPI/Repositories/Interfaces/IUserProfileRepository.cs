using UserAPI.Models;

namespace UserAPI.Repositories.Interfaces;

public interface IUserProfileRepository
{
    /// <summary>Lấy hồ sơ theo AccountId (tham chiếu logic tới Auth). Null nếu chưa có hồ sơ.</summary>
    Task<UserProfile?> GetByAccountIdAsync(Guid accountId);

    /// <summary>Lấy hồ sơ có theo dõi thay đổi (tracking) để cập nhật.</summary>
    Task<UserProfile?> GetTrackedByAccountIdAsync(Guid accountId);

    /// <summary>Lấy nhiều hồ sơ theo danh sách AccountId (phục vụ resolve FullName cho service khác).</summary>
    Task<List<UserProfile>> GetByAccountIdsAsync(IReadOnlyCollection<Guid> accountIds);

    /// <summary>Tìm AccountId theo FullName chứa keyword (phục vụ search theo tên ở service khác).</summary>
    Task<List<Guid>> SearchAccountIdsByNameAsync(string keyword);

    /// <summary>Kiểm tra đã có hồ sơ cho AccountId hay chưa (để tạo idempotent).</summary>
    Task<bool> ExistsByAccountIdAsync(Guid accountId);

    Task AddAsync(UserProfile profile);

    Task<int> SaveChangesAsync();
}

using AuthAPI.Models;

namespace AuthAPI.Repositories.Interfaces;

public interface IAccountRepository
{
    Task<bool> ExistsByEmailAsync(string email);

    Task<Account?> GetByEmailAsync(string email);

    Task<Account?> GetByIdAsync(Guid accountId);

    /// <summary>Lấy nhiều tài khoản theo danh sách Id (phục vụ resolve email cho service khác).</summary>
    Task<List<Account>> GetByIdsAsync(IReadOnlyCollection<Guid> accountIds);

    Task AddAsync(Account account);

    /// <summary>
    /// Lưu mọi thay đổi đang được DbContext theo dõi (đóng vai trò unit-of-work).
    /// </summary>
    Task<int> SaveChangesAsync();
}

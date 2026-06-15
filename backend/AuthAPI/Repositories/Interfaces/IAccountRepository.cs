using AuthAPI.Models;

namespace AuthAPI.Repositories.Interfaces;

public interface IAccountRepository
{
    Task<bool> ExistsByEmailAsync(string email);

    Task<Account?> GetByEmailAsync(string email);

    Task<Account?> GetByIdAsync(Guid accountId);

    Task AddAsync(Account account);

    /// <summary>
    /// Lưu mọi thay đổi đang được DbContext theo dõi (đóng vai trò unit-of-work).
    /// </summary>
    Task<int> SaveChangesAsync();
}

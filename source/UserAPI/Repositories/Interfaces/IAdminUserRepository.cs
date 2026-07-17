

using UserAPI.Models;

namespace UserAPI.Repositories.Interfaces;

public interface IAdminUserRepository
{
    Task<UserProfile?> GetProfileByAccountIdAsync(Guid accountId);
    Task<UserProfile?> GetProfileByUserIdAsync(Guid userId);
    Task<List<UserProfile>> GetProfilesByAccountIdsAsync(List<Guid> accountIds);
    
    Task<IEnumerable<Guid>> GetAccountIdsByNameKeywordAsync(string keyword);
}
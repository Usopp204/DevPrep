

using Microsoft.EntityFrameworkCore;
using UserAPI.Models;
using UserAPI.Repositories.Interfaces;

namespace UserAPI.Repositories;

public class AdminUserRepository : IAdminUserRepository
{
    private readonly UserDbContext _context;

    public AdminUserRepository(UserDbContext context)
    {
        _context = context;
    }

    public async Task<UserProfile?> GetProfileByAccountIdAsync(Guid accountId)
    {
        return await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.AccountId == accountId);
    }

    public async Task<UserProfile?> GetProfileByUserIdAsync(Guid userId)
    {
        return await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task<List<UserProfile>> GetProfilesByAccountIdsAsync(List<Guid> accountIds)
    {
        return await _context.UserProfiles
            .Where(p => accountIds.Contains(p.AccountId))
            .ToListAsync();
    }

    public async Task<IEnumerable<Guid>> GetAccountIdsByNameKeywordAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Enumerable.Empty<Guid>();
        }

        var lowerKeyword = keyword.ToLower();

        // Tìm kiếm các UserProfile có FullName chứa từ khóa và trả về danh sách AccountId tương ứng
        return await _context.UserProfiles
            .Where(p => p.FullName != null && p.FullName.ToLower().Contains(lowerKeyword))
            .Select(p => p.AccountId)
            .ToListAsync();
    }
}
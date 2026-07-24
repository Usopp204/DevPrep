using Microsoft.EntityFrameworkCore;
using UserAPI.Models;
using UserAPI.Repositories.Interfaces;

namespace UserAPI.Repositories;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly UserDbContext _context;

    public UserProfileRepository(UserDbContext context)
    {
        _context = context;
    }

    public Task<UserProfile?> GetByAccountIdAsync(Guid accountId) =>
        _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.AccountId == accountId);

    public Task<UserProfile?> GetTrackedByAccountIdAsync(Guid accountId) =>
        _context.UserProfiles
            .FirstOrDefaultAsync(p => p.AccountId == accountId);

    public Task<List<UserProfile>> GetByAccountIdsAsync(IReadOnlyCollection<Guid> accountIds) =>
        _context.UserProfiles
            .AsNoTracking()
            .Where(p => accountIds.Contains(p.AccountId))
            .ToListAsync();

    public Task<List<Guid>> SearchAccountIdsByNameAsync(string keyword) =>
        _context.UserProfiles
            .AsNoTracking()
            .Where(p => p.FullName != null && p.FullName.Contains(keyword))
            .Select(p => p.AccountId)
            .ToListAsync();

    public Task<bool> ExistsByAccountIdAsync(Guid accountId) =>
        _context.UserProfiles.AnyAsync(p => p.AccountId == accountId);

    public async Task AddAsync(UserProfile profile) =>
        await _context.UserProfiles.AddAsync(profile);

    public async Task<int> DeleteByAccountIdsAsync(IReadOnlyCollection<Guid> accountIds)
    {
        var profiles = await _context.UserProfiles
            .Where(p => accountIds.Contains(p.AccountId))
            .ToListAsync();
        if (profiles.Count == 0)
        {
            return 0;
        }

        _context.UserProfiles.RemoveRange(profiles);
        return await _context.SaveChangesAsync();
    }

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
}

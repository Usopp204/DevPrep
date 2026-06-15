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

    public Task<bool> ExistsByAccountIdAsync(Guid accountId) =>
        _context.UserProfiles.AnyAsync(p => p.AccountId == accountId);

    public async Task AddAsync(UserProfile profile) =>
        await _context.UserProfiles.AddAsync(profile);

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
}

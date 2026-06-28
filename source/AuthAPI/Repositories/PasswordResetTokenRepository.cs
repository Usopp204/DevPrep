using AuthAPI.Helpers;
using AuthAPI.Models;
using AuthAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthAPI.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly AuthDbContext _context;

    public PasswordResetTokenRepository(AuthDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PasswordResetToken token) =>
        await _context.PasswordResetTokens.AddAsync(token);

    public Task<PasswordResetToken?> GetValidByTokenAsync(string token) =>
        _context.PasswordResetTokens
            .Include(t => t.Account)
            .FirstOrDefaultAsync(t =>
                t.Token == token &&
                !t.IsUsed &&
                t.ExpiresAt > VietnamTime.Now);

    public Task<List<PasswordResetToken>> GetUnusedTokensByAccountAsync(Guid accountId) =>
        _context.PasswordResetTokens
            .Where(t => t.AccountId == accountId && !t.IsUsed)
            .ToListAsync();
}

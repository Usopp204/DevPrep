using AuthAPI.Helpers;
using AuthAPI.Models;
using AuthAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthAPI.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AuthDbContext _context;

    public RefreshTokenRepository(AuthDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RefreshToken token) =>
        await _context.RefreshTokens.AddAsync(token);

    public Task<RefreshToken?> GetByTokenAsync(string token) =>
        _context.RefreshTokens
            .Include(t => t.Account)
            .FirstOrDefaultAsync(t => t.Token == token);

    public async Task RevokeAllActiveByAccountAsync(Guid accountId)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.AccountId == accountId && !t.IsRevoked)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = VietnamTime.Now;
        }
    }
}

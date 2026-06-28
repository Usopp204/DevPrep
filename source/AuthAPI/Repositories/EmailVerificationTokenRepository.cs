using AuthAPI.Helpers;
using AuthAPI.Models;
using AuthAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthAPI.Repositories;

public class EmailVerificationTokenRepository : IEmailVerificationTokenRepository
{
    private readonly AuthDbContext _context;

    public EmailVerificationTokenRepository(AuthDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(EmailVerificationToken token) =>
        await _context.EmailVerificationTokens.AddAsync(token);

    public Task<EmailVerificationToken?> GetValidTokenAsync(Guid accountId, string token) =>
        _context.EmailVerificationTokens.FirstOrDefaultAsync(t =>
            t.AccountId == accountId &&
            t.Token == token &&
            !t.IsUsed &&
            t.ExpiresAt > VietnamTime.Now);

    public Task<List<EmailVerificationToken>> GetUnusedTokensByAccountAsync(Guid accountId) =>
        _context.EmailVerificationTokens
            .Where(t => t.AccountId == accountId && !t.IsUsed)
            .ToListAsync();
}

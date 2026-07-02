using AuthAPI.Models;
using AuthAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthAPI.Repositories;

public class SsoIdentityRepository : ISsoIdentityRepository
{
    private readonly AuthDbContext _context;

    public SsoIdentityRepository(AuthDbContext context)
    {
        _context = context;
    }

    public Task<SsoIdentity?> GetByProviderAsync(string provider, string providerUserId) =>
        _context.SsoIdentities.FirstOrDefaultAsync(s =>
            s.Provider == provider && s.ProviderUserId == providerUserId);

    public async Task AddAsync(SsoIdentity identity) =>
        await _context.SsoIdentities.AddAsync(identity);
}

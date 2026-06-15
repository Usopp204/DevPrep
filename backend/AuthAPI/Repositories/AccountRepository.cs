using AuthAPI.Models;
using AuthAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthAPI.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly AuthDbContext _context;

    public AccountRepository(AuthDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExistsByEmailAsync(string email) =>
        _context.Accounts.AnyAsync(a => a.Email == email);

    public Task<Account?> GetByEmailAsync(string email) =>
        _context.Accounts.FirstOrDefaultAsync(a => a.Email == email);

    public Task<Account?> GetByIdAsync(Guid accountId) =>
        _context.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);

    public async Task AddAsync(Account account) =>
        await _context.Accounts.AddAsync(account);

    public Task<int> SaveChangesAsync() =>
        _context.SaveChangesAsync();
}

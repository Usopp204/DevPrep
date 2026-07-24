using AuthAPI.Helpers;
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

    public Task<List<Account>> GetByIdsAsync(IReadOnlyCollection<Guid> accountIds) =>
        _context.Accounts
            .AsNoTracking()
            .Where(a => accountIds.Contains(a.AccountId))
            .ToListAsync();

    public Task<List<Guid>> SearchAccountIdsByEmailAsync(string keyword) =>
        _context.Accounts
            .AsNoTracking()
            .Where(a => a.Email.Contains(keyword))
            .Select(a => a.AccountId)
            .ToListAsync();

    public async Task AddAsync(Account account) =>
        await _context.Accounts.AddAsync(account);

    public Task<List<Account>> GetExpiredPendingAccountsAsync(int maxCount)
    {
        // "Pending" + chưa verify + không còn token xác thực nào hợp lệ -> hết hạn đăng ký.
        // Điều kiện dựa trên token (không phải CreatedAt) để Resend OTP tự kéo dài sự sống account.
        var now = VietnamTime.Now;
        return _context.Accounts
            .Where(a => a.Status == "Pending" && !a.IsEmailVerified &&
                !_context.EmailVerificationTokens.Any(t =>
                    t.AccountId == a.AccountId && !t.IsUsed && t.ExpiresAt > now))
            .Take(maxCount)
            .ToListAsync();
    }

    public async Task<bool> TryDeleteExpiredPendingAsync(Guid accountId)
    {
        // ExecuteDelete: điều kiện "hết hạn đăng ký" nằm ngay trong câu DELETE nên nguyên tử —
        // account vừa được resend thêm token mới (hoặc đã bị xoá) thì 0 dòng bị ảnh hưởng.
        var now = VietnamTime.Now;
        var deleted = await _context.Accounts
            .Where(a => a.AccountId == accountId && a.Status == "Pending" && !a.IsEmailVerified &&
                !_context.EmailVerificationTokens.Any(t =>
                    t.AccountId == a.AccountId && !t.IsUsed && t.ExpiresAt > now))
            .ExecuteDeleteAsync();
        return deleted > 0;
    }

    public Task<int> SaveChangesAsync() =>
        _context.SaveChangesAsync();
}

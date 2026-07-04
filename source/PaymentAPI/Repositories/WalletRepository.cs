using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.Models;

namespace PaymentAPI.Repositories;

public class WalletRepository : Repository<CreditWallet>, IWalletRepository
{
    public WalletRepository(PaymentDbContext db) : base(db) { }

    public async Task<CreditWallet?> GetByUserIdAsync(Guid userId)
        => await _set.FirstOrDefaultAsync(w => w.UserId == userId);
}

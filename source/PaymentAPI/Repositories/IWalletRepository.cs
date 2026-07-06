using PaymentAPI.Models;

namespace PaymentAPI.Repositories;

public interface IWalletRepository : IRepository<CreditWallet>
{
    Task<CreditWallet?> GetByUserIdAsync(Guid userId);
}

using PaymentAPI.Models;

namespace PaymentAPI.Repositories;

public interface ITransactionRepository : IRepository<PaymentTransaction>
{
    Task<PaymentTransaction?> GetByOrderIdAsync(Guid orderId);
}

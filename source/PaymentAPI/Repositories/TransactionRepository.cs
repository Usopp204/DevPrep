using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.Models;

namespace PaymentAPI.Repositories;

public class TransactionRepository : Repository<PaymentTransaction>, ITransactionRepository
{
    public TransactionRepository(PaymentDbContext db) : base(db) { }

    public async Task<PaymentTransaction?> GetByOrderIdAsync(Guid orderId)
        => await _set.FirstOrDefaultAsync(t => t.OrderId == orderId);
}

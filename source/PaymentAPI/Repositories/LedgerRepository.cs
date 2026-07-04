using PaymentAPI.Data;
using PaymentAPI.Models;

namespace PaymentAPI.Repositories;

public class LedgerRepository : Repository<CreditLedgerEntry>, ILedgerRepository
{
    public LedgerRepository(PaymentDbContext db) : base(db) { }
}

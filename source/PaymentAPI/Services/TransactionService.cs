using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.DTOs;

namespace PaymentAPI.Services;

public class TransactionService : ITransactionService
{
    private readonly PaymentDbContext _db;

    public TransactionService(PaymentDbContext db)
    {
        _db = db;
    }

    public async Task<TransactionHistoryResultDto> GetHistoryAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Orders.Where(o => o.UserId == userId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new TransactionDto
            {
                OrderId = o.OrderId,
                OrderCode = o.OrderCode,
                PaymentMethod = o.PaymentMethod,
                Credits = o.Credits,
                Amount = o.Amount,
                Currency = o.Currency,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                PaidAt = o.PaidAt,
            })
            .ToListAsync(ct);

        var wallet = await _db.CreditWallets.FirstOrDefaultAsync(w => w.UserId == userId, ct);
        var totalPurchased = await _db.Orders
            .Where(o => o.UserId == userId && o.Status == "Paid")
            .SumAsync(o => (int?)o.Credits, ct) ?? 0;

        return new TransactionHistoryResultDto
        {
            Balance = new WalletBalanceDto
            {
                Balance = wallet?.Balance ?? 0,
                TotalPurchased = totalPurchased,
            },
            Transactions = new PagedResult<TransactionDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            },
        };
    }
}

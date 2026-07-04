using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;

namespace PaymentAPI.Repositories;

public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(PaymentDbContext db) : base(db) { }

    public async Task<Order?> GetByOrderCodeAsync(string orderCode)
        => await _set.FirstOrDefaultAsync(o => o.OrderCode == orderCode);

    public async Task<PagedResult<Order>> GetPagedByUserAsync(Guid userId, int page, int pageSize)
    {
        var q = _set.Where(o => o.UserId == userId && o.Status != "Pending")
                    .OrderByDescending(o => o.CreatedAt);
        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<Order> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<PagedResult<Order>> GetPagedAllAsync(int page, int pageSize)
    {
        var q = _set.OrderByDescending(o => o.CreatedAt);
        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<Order> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<int> GetTotalCreditsByUserAsync(Guid userId)
        => await _set.Where(o => o.UserId == userId && o.Status == "Paid")
                     .SumAsync(o => (int?)o.Credits) ?? 0;
}

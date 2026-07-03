using PaymentAPI.DTOs;
using PaymentAPI.Models;

namespace PaymentAPI.Repositories;

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetByOrderCodeAsync(string orderCode);
    Task<PagedResult<Order>> GetPagedByUserAsync(Guid userId, int page, int pageSize);
    Task<PagedResult<Order>> GetPagedAllAsync(int page, int pageSize);
    Task<int> GetTotalCreditsByUserAsync(Guid userId);
}

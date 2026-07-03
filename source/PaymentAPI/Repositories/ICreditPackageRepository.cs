using PaymentAPI.DTOs;
using PaymentAPI.Models;

namespace PaymentAPI.Repositories;

public interface ICreditPackageRepository : IRepository<CreditPackage>
{
    Task<IEnumerable<CreditPackage>> GetActiveAsync();
    Task<PagedResult<CreditPackage>> GetPagedAsync(string? keyword, bool? isActive, int page, int pageSize);
    Task<bool> ExistsActiveNameAsync(string name, Guid? excludeId = null);
}

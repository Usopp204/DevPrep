using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;

namespace PaymentAPI.Repositories;

public class CreditPackageRepository : Repository<CreditPackage>, ICreditPackageRepository
{
    public CreditPackageRepository(PaymentDbContext db) : base(db) { }

    public async Task<IEnumerable<CreditPackage>> GetActiveAsync()
        => await _set.Where(p => p.IsActive).OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt).ToListAsync();

    public async Task<PagedResult<CreditPackage>> GetPagedAsync(
        string? keyword, bool? isActive, int page, int pageSize)
    {
        var q = _set.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            q = q.Where(p => p.Name.ToLower().Contains(kw) ||
                              (p.Description != null && p.Description.ToLower().Contains(kw)));
        }

        if (isActive.HasValue)
            q = q.Where(p => p.IsActive == isActive.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return new PagedResult<CreditPackage>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<bool> ExistsActiveNameAsync(string name, Guid? excludeId = null)
        => await _set.AnyAsync(p => p.IsActive && p.Name == name &&
                                    (excludeId == null || p.PackageId != excludeId));
}

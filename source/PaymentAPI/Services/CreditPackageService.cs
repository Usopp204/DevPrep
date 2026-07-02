using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.DTOs;

namespace PaymentAPI.Services;

public class CreditPackageService : ICreditPackageService
{
    private readonly PaymentDbContext _db;

    public CreditPackageService(PaymentDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<CreditPackageDto>> GetListAsync(int page, int pageSize, string? keyword, bool? isActive)
    {
        var query = _db.CreditPackages.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(kw) ||
                                     (p.Description != null && p.Description.ToLower().Contains(kw)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new CreditPackageDto
            {
                PackageId    = p.PackageId,
                Name         = p.Name,
                Description  = p.Description,
                Credits      = p.Credits,
                BonusCredits = p.BonusCredits,
                Price        = p.Price,
                Currency     = p.Currency,
                IsActive     = p.IsActive,
                SortOrder    = p.SortOrder,
                CreatedAt    = p.CreatedAt,
                UpdatedAt    = p.UpdatedAt,
            })
            .ToListAsync();

        return new PagedResult<CreditPackageDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    public async Task<CreditPackageDto?> GetByIdAsync(Guid id)
    {
        var p = await _db.CreditPackages.FindAsync(id);
        if (p is null) return null;

        return new CreditPackageDto
        {
            PackageId    = p.PackageId,
            Name         = p.Name,
            Description  = p.Description,
            Credits      = p.Credits,
            BonusCredits = p.BonusCredits,
            Price        = p.Price,
            Currency     = p.Currency,
            IsActive     = p.IsActive,
            SortOrder    = p.SortOrder,
            CreatedAt    = p.CreatedAt,
            UpdatedAt    = p.UpdatedAt,
        };
    }
}

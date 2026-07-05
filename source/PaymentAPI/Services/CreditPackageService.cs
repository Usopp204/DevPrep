using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Repositories;

namespace PaymentAPI.Services;

public sealed class CreditPackageService(ICreditPackageRepository repo) : ICreditPackageService
{
    public async Task<PagedResult<CreditPackageDto>> GetListAsync(
        int page, int pageSize, string? keyword, bool? isActive)
    {
        var result = await repo.GetPagedAsync(keyword, isActive, page, pageSize);
        return new PagedResult<CreditPackageDto>
        {
            Items      = result.Items.Select(ToDto).ToList(),
            TotalCount = result.TotalCount,
            Page       = result.Page,
            PageSize   = result.PageSize,
        };
    }

    public async Task<CreditPackageDto?> GetByIdAsync(Guid id)
    {
        var p = await repo.GetByIdAsync(id);
        return p is null ? null : ToDto(p);
    }

    private static CreditPackageDto ToDto(CreditPackage p) => new()
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

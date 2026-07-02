using PaymentAPI.DTOs;

namespace PaymentAPI.Services;

public interface ICreditPackageService
{
    Task<PagedResult<CreditPackageDto>> GetListAsync(int page, int pageSize, string? keyword, bool? isActive);
    Task<CreditPackageDto?> GetByIdAsync(Guid id);
}

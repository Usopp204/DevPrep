using UserAPI.DTOs;
using UserAPI.Repositories.Interfaces;
using UserAPI.Services.Interfaces;

namespace UserAPI.Services;

public class AdminUserService : IAdminUserService
{
    private readonly IAdminUserRepository _repository;

    public AdminUserService(IAdminUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(AdminUserQueryDto query)
    {
        query.Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim();
        query.Role = string.IsNullOrWhiteSpace(query.Role) ? null : query.Role.Trim();
        query.Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim();

        if (query.Page <= 0)
            query.Page = 1;

        if (query.PageSize <= 0 || query.PageSize > 100)
            query.PageSize = 10;

        return await _repository.GetUsersAsync(query);
    }

    public async Task<AdminUserDetailDto?> GetUserDetailAsync(Guid id)
    {
        return await _repository.GetUserDetailAsync(id);
    }

    public async Task<bool> UpdateStatusAsync(Guid id, AdminUpdateUserStatusDto dto, Guid? adminAccountId)
    {
        dto.Status = dto.Status?.Trim() ?? string.Empty;
        dto.Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();

        var allowedStatuses = new[] { "Active", "Blocked" };

        if (!allowedStatuses.Contains(dto.Status, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Status chỉ được là Active hoặc Blocked.");
        }

        var normalizedStatus = allowedStatuses
            .First(x => string.Equals(x, dto.Status, StringComparison.OrdinalIgnoreCase));

        dto.Status = normalizedStatus;

        return await _repository.UpdateStatusAsync(id, dto, adminAccountId);
    }

    public async Task<bool> UpdateRoleAsync(Guid id, AdminUpdateUserRoleDto dto)
    {
        dto.Role = dto.Role?.Trim() ?? string.Empty;

        var allowedRoles = new[] { "Admin", "Manager", "User" };

        if (!allowedRoles.Contains(dto.Role, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Role chỉ được là Admin, Manager hoặc User.");
        }

        var normalizedRole = allowedRoles
            .First(x => string.Equals(x, dto.Role, StringComparison.OrdinalIgnoreCase));

        dto.Role = normalizedRole;

        return await _repository.UpdateRoleAsync(id, dto);
    }
    public async Task<AdminAutoBlockInactiveResultDto> AutoBlockInactiveUsersAsync(int inactiveDays)
    {
        if (inactiveDays < 1)
            inactiveDays = 30;

        if (inactiveDays > 365)
            inactiveDays = 365;

        return await _repository.AutoBlockInactiveUsersAsync(inactiveDays);
    }
}
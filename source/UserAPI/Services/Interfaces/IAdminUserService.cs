using UserAPI.DTOs;

namespace UserAPI.Services.Interfaces;

public interface IAdminUserService
{
    Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(AdminUserQueryDto query);

    Task<AdminUserDetailDto?> GetUserDetailAsync(Guid id);

    Task<bool> UpdateStatusAsync(Guid id, AdminUpdateUserStatusDto dto, Guid? adminAccountId);

    Task<bool> UpdateRoleAsync(Guid id, AdminUpdateUserRoleDto dto);
    Task<AdminAutoBlockInactiveResultDto> AutoBlockInactiveUsersAsync(int inactiveDays);
}
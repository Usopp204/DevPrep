using UserAPI.DTOs;

namespace UserAPI.Services.Interfaces;

public interface IAdminUserService
{
    Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(AdminUserQueryDto query);

    Task<CandidateStatisticsDto> GetCandidateStatisticsAsync(DateOnly from, DateOnly to, string authorizationHeader, CancellationToken cancellationToken);

    Task<AdminUserDetailDto?> GetUserDetailAsync(Guid id);

    
    Task<bool> UpdateStatusAsync(Guid id, AdminUpdateUserStatusDto dto, Guid? adminAccountId);
    Task<bool> UpdateRoleAsync(Guid id, AdminUpdateUserRoleDto dto, Guid? adminAccountId);
    Task<AdminAutoBlockInactiveResultDto> AutoBlockInactiveUsersAsync(int inactiveDays);
}
using System;
using System.Threading.Tasks;
using WebClient.Models.Manager;
using WebClient.Models.Shared;

namespace WebClient.Services.Manager;

public interface IManagerUserService
{
    Task<PagedResult<UserListItemViewModel>> GetListAsync(int page = 1, int pageSize = 10, string? keyword = null, string? role = null, string? status = null, DateTime? createdFrom = null, DateTime? createdTo = null);
    Task<UserDetailsViewModel?> GetByIdAsync(Guid id);
}

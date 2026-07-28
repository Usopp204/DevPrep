using WebClient.Models.Manager;

namespace WebClient.Services.Manager;

public interface IManagerNewsService
{
    Task<ManagerNewsPagedResult<ManagerNewsListItemViewModel>> GetListAsync(
        int page = 1,
        int pageSize = 10,
        string? keyword = null,
        string? status = null,
        Guid? categoryId = null);

    Task<ManagerNewsDetailViewModel?> GetByIdAsync(Guid id);

    Task<List<ManagerNewsCategoryViewModel>> GetCategoriesAsync();
}

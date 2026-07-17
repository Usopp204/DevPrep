using WebClient.Models.Manager;
using WebClient.Models.Shared;

namespace WebClient.Services.Manager;

public interface IManagerQuestionService
{
    Task<PagedResult<QuestionBankListItemViewModel>> GetListAsync(int page = 1, int pageSize = 10, string? keyword = null, string? questionType = null, string? level = null, string? difficulty = null, string? status = null, Guid? categoryId = null);
    Task<QuestionBankDetailViewModel?> GetByIdAsync(Guid id);
    Task<List<QuestionBankCategoryViewModel>> GetCategoriesAsync();
}

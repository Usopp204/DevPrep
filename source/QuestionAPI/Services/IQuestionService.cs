using QuestionAPI.DTOs;

namespace QuestionAPI.Services;

public interface IQuestionService
{
    Task<PagedResult<QuestionListItemDto>> GetListAsync(
        int page, int pageSize,
        string? keyword,
        string? questionType,
        string? level,
        string? difficulty,
        string? status,
        Guid? categoryId);

    Task<QuestionDetailDto?> GetByIdAsync(Guid id);

    Task<List<QuestionCategoryDto>> GetCategoriesAsync();
}

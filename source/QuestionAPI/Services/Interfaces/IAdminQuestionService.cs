using QuestionAPI.DTOs;

namespace QuestionAPI.Services.Interfaces;

public interface IAdminQuestionService
{
    Task<AdminPagedResultDto<AdminQuestionListItemDto>> GetAsync(AdminQuestionQueryDto q);

    Task<AdminPagedResultDto<AdminQuestionListItemDto>> SearchAsync(AdminQuestionQueryDto q);

    Task<AdminPagedResultDto<AdminQuestionListItemDto>> FilterAsync(AdminQuestionQueryDto q);

    Task<AdminQuestionDetailDto?> GetDetailAsync(Guid id);

    Task<AdminQuestionDetailDto> CreateAsync(AdminCreateQuestionDto dto, Guid? adminId);

    Task<AdminQuestionDetailDto?> UpdateAsync(Guid id, AdminUpdateQuestionDto dto, Guid? adminId);

    Task<bool> DeleteAsync(Guid id, Guid? adminId);

    Task<bool> RestoreAsync(Guid id);

    Task<List<AdminCategoryDto>> GetAllCategoriesAsync();
}
using QuestionAPI.Models;

namespace QuestionAPI.Repositories.Interfaces;

public interface IAdminQuestionRepository
{
    IQueryable<Question> Query();

    Task<Question?> GetByIdAsync(Guid id);

    Task<Question?> GetTrackedAsync(Guid id);

    Task AddAsync(Question q);

    Task SaveAsync();
}
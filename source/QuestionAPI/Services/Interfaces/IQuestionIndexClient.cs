using QuestionAPI.Models;

namespace QuestionAPI.Services.Interfaces;

public interface IQuestionIndexClient
{
    Task<bool> UpsertAsync(Question question, string? categoryName, string? positionName);
    Task<bool> DeleteAsync(Guid questionId);
    Task<bool> ResetAsync();
}

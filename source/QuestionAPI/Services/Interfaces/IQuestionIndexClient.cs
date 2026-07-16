using QuestionAPI.Models;

namespace QuestionAPI.Services.Interfaces;

public interface IQuestionIndexClient
{
    Task<bool> UpsertAsync(Question question, string? categoryName);
    Task<bool> DeleteAsync(Guid questionId);
}
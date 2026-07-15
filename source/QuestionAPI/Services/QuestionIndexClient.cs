using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using QuestionAPI.Models;
using QuestionAPI.Services.Interfaces;

namespace QuestionAPI.Services;

public class QuestionIndexClient : IQuestionIndexClient
{
    private readonly HttpClient _http;
    private readonly ILogger<QuestionIndexClient> _logger;

    public QuestionIndexClient(HttpClient http, ILogger<QuestionIndexClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> UpsertAsync(Question question, string? categoryName)
    {
        try
        {
            var payload = new
            {
                question_id = question.QuestionId.ToString(),
                question = question.QuestionText,
                answer = question.Answer,
                category = categoryName,
                difficulty = question.Difficulty,
                position = question.Position
            };

            var response = await _http.PostAsJsonAsync("internal/questions/index/upsert", payload);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Indexed question {QuestionId} to ChromaDB.", question.QuestionId);
                return true;
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "AI index failed. QuestionId={QuestionId}, Status={Status}, Body={Body}",
                question.QuestionId,
                response.StatusCode,
                body
            );
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not connect to AI Service when indexing question {QuestionId}.",
                question.QuestionId
            );
            return false;
        }
    }

    public async Task<bool> DeleteAsync(Guid questionId)
    {
        try
        {
            var response = await _http.DeleteAsync($"internal/questions/index/{questionId}");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Removed question {QuestionId} from ChromaDB.", questionId);
                return true;
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "AI delete failed. QuestionId={QuestionId}, Status={Status}, Body={Body}",
                questionId,
                response.StatusCode,
                body
            );
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not connect to AI Service when deleting question {QuestionId}.",
                questionId
            );
            return false;
        }
    }
}
using System.Net.Http.Json;
using EvaluationAPI.DTOs.AiService;

namespace EvaluationAPI.Clients;

public class AiServiceClient : IAiServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiServiceClient> _logger;

    public AiServiceClient(HttpClient httpClient, ILogger<AiServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AiQuestionPlanResponse> PlanQuestionsAsync(
        AiQuestionPlanRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/internal/questions/plan", request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AiQuestionPlanResponse>(ct)
                   ?? new AiQuestionPlanResponse();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Call to ai-service POST /internal/questions/plan failed.");
            throw;
        }
    }

    public async Task<AiEvaluateAnswerResponse> EvaluateAnswerAsync(
        AiEvaluateAnswerRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/internal/evaluate-answer", request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AiEvaluateAnswerResponse>(ct)
                   ?? new AiEvaluateAnswerResponse();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Call to ai-service POST /internal/evaluate-answer failed.");
            throw;
        }
    }

    public async Task<AiBuildReportResponse> BuildReportAsync(
        AiBuildReportRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/internal/report", request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AiBuildReportResponse>(ct)
                   ?? new AiBuildReportResponse();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Call to ai-service POST /internal/report failed.");
            throw;
        }
    }
}

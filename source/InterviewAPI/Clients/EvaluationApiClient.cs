using System.Net.Http.Json;
using InterviewAPI.DTOs.Evaluation;

namespace InterviewAPI.Clients;

public class EvaluationApiClient : IEvaluationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EvaluationApiClient> _logger;

    public EvaluationApiClient(HttpClient httpClient, ILogger<EvaluationApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task EvaluateAnswerAsync(
        Guid sessionId, Guid sessionQuestionId, EvaluateAnswerPayload payload,
        string? bearerToken, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"api/evaluation/sessions/{sessionId}/answers/{sessionQuestionId}");
            request.Content = JsonContent.Create(payload);
            if (!string.IsNullOrEmpty(bearerToken))
                request.Headers.TryAddWithoutValidation("Authorization", bearerToken);

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Call to EvaluationAPI evaluate for question {QuestionId} in session {SessionId} failed.",
                sessionQuestionId, sessionId);
            throw;
        }
    }
}

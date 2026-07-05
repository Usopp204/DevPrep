using System.Net.Http.Headers;
using System.Net.Http.Json;
using InterviewAPI.DTOs.AiService;

namespace InterviewAPI.Clients;

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

    public async Task<byte[]> SynthesizeSpeechAsync(string text, CancellationToken ct = default)
    {
        try
        {
            // The TTS route lives under the /interview router prefix (stateless, body = { text }).
            var response = await _httpClient.PostAsJsonAsync("/interview/tts", new { text }, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Call to ai-service POST /tts failed.");
            throw;
        }
    }

    public async Task<string> TranscribeAsync(
        byte[] audio, string fileName, string contentType, CancellationToken ct = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(audio);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "audio", fileName);

            var response = await _httpClient.PostAsync("/internal/stt", content, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AiSttResponse>(ct);
            return result?.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Call to ai-service POST /internal/stt failed.");
            throw;
        }
    }

    public async Task<string> GetHintAsync(string question, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/internal/hint", new { question }, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AiHintResponse>(ct);
            return result?.Hint ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Call to ai-service POST /internal/hint failed.");
            throw;
        }
    }

    public async Task<AiCatalogResponse> GetCatalogAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/catalog", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AiCatalogResponse>(ct)
                   ?? new AiCatalogResponse();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Call to ai-service GET /catalog failed.");
            throw;
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

    public Task<BiometricEmbeddingResult> EmbedFaceAsync(IFormFile file, CancellationToken ct = default) =>
        EmbedAsync("/internal/biometrics/face/embed", file, ct);

    public Task<BiometricEmbeddingResult> EmbedVoiceAsync(IFormFile file, CancellationToken ct = default) =>
        EmbedAsync("/internal/biometrics/voice/embed", file, ct);

    private async Task<BiometricEmbeddingResult> EmbedAsync(string path, IFormFile file, CancellationToken ct)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            await using var stream = file.OpenReadStream();
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);
            content.Add(fileContent, "file", string.IsNullOrEmpty(file.FileName) ? "sample" : file.FileName);

            var response = await _httpClient.PostAsync(path, content, ct);
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                // Quality rejection: surface ai-service's reason (e.g. "No face detected").
                throw new BiometricQualityException(await ReadDetailAsync(response, ct));
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<BiometricEmbeddingResult>(ct)
                   ?? throw new InvalidOperationException("Empty response from ai-service embed endpoint.");
        }
        catch (BiometricQualityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Call to ai-service POST {Path} failed.", path);
            throw;
        }
    }

    public async Task<AiFaceVerifyLiveResponse> VerifyFaceLiveAsync(
        IReadOnlyList<IFormFile> frames, float[] storedEmbedding, string challengeJson,
        CancellationToken ct = default)
    {
        const string path = "/internal/biometrics/face/verify-live";
        try
        {
            using var content = new MultipartFormDataContent();
            var streams = new List<Stream>();
            try
            {
                foreach (var frame in frames)
                {
                    var stream = frame.OpenReadStream();
                    streams.Add(stream);
                    var fileContent = new StreamContent(stream);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                        string.IsNullOrEmpty(frame.ContentType) ? "image/jpeg" : frame.ContentType);
                    content.Add(fileContent, "frames",
                        string.IsNullOrEmpty(frame.FileName) ? "frame.jpg" : frame.FileName);
                }
                content.Add(new StringContent(JsonSerializer.Serialize(storedEmbedding)), "stored_embedding");
                content.Add(new StringContent(challengeJson), "challenge");

                var response = await _httpClient.PostAsync(path, content, ct);
                if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    throw new BiometricQualityException(await ReadDetailAsync(response, ct));
                }
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<AiFaceVerifyLiveResponse>(ct)
                       ?? throw new InvalidOperationException("Empty response from ai-service verify-live endpoint.");
            }
            finally
            {
                foreach (var stream in streams)
                {
                    await stream.DisposeAsync();
                }
            }
        }
        catch (BiometricQualityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Call to ai-service POST {Path} failed.", path);
            throw;
        }
    }

    public async Task<AiVoiceVerifyChallengeResponse> VerifyVoiceChallengeAsync(
        IFormFile file, float[] storedEmbedding, string expectedPhrase,
        CancellationToken ct = default)
    {
        const string path = "/internal/biometrics/voice/verify-challenge";
        try
        {
            using var content = new MultipartFormDataContent();
            await using var stream = file.OpenReadStream();
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrEmpty(file.ContentType) ? "audio/webm" : file.ContentType);
            content.Add(fileContent, "file", string.IsNullOrEmpty(file.FileName) ? "voice.webm" : file.FileName);
            content.Add(new StringContent(JsonSerializer.Serialize(storedEmbedding)), "stored_embedding");
            content.Add(new StringContent(expectedPhrase), "expected_phrase");

            var response = await _httpClient.PostAsync(path, content, ct);
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                throw new BiometricQualityException(await ReadDetailAsync(response, ct));
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AiVoiceVerifyChallengeResponse>(ct)
                   ?? throw new InvalidOperationException("Empty response from ai-service verify-challenge endpoint.");
        }
        catch (BiometricQualityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Call to ai-service POST {Path} failed.", path);
            throw;
        }
    }

    // FastAPI errors look like { "detail": "..." }.
    private static async Task<string> ReadDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var raw = await response.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("detail", out var detail)
                && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString()!;
            }
        }
        catch (JsonException)
        {
        }
        return "Biometric sample was rejected.";
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

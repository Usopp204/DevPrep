using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WebClient.Models.Interview;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.Interview;

// Separate from InterviewService on purpose (additive file, no merge friction with
// in-flight local changes there). Same IApiClient + envelope pattern.
public interface IVerificationService
{
    Task<(bool Success, VerificationChallengeModel? Challenge, string? Error)> GetChallengeAsync(
        Guid sessionId, string modality, CancellationToken cancellationToken);

    Task<(bool Success, VerificationResultModel? Result, string? Error)> VerifyFaceAsync(
        Guid sessionId, IReadOnlyList<(byte[] Data, string FileName, string ContentType)> frames,
        CancellationToken cancellationToken);

    Task<(bool Success, VerificationResultModel? Result, string? Error)> VerifyVoiceAsync(
        Guid sessionId, byte[] audio, string fileName, string contentType,
        CancellationToken cancellationToken);
}

public sealed class VerificationService(IApiClient apiClient) : IVerificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<(bool Success, VerificationChallengeModel? Challenge, string? Error)> GetChallengeAsync(
        Guid sessionId, string modality, CancellationToken cancellationToken)
    {
        using var response = await apiClient.HttpClient.GetAsync(
            $"api/interview/sessions/{sessionId}/verify/challenge?modality={Uri.EscapeDataString(modality)}",
            cancellationToken);
        var envelope = await ReadEnvelopeAsync<VerificationChallengeModel>(response, cancellationToken);
        if (!response.IsSuccessStatusCode || envelope is null || !envelope.Success || envelope.Data is null)
        {
            return (false, null, envelope?.Message ?? $"Could not get a challenge ({(int)response.StatusCode}).");
        }

        return (true, envelope.Data, null);
    }

    public async Task<(bool Success, VerificationResultModel? Result, string? Error)> VerifyFaceAsync(
        Guid sessionId, IReadOnlyList<(byte[] Data, string FileName, string ContentType)> frames,
        CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        foreach (var (data, fileName, contentType) in frames)
        {
            var fileContent = new ByteArrayContent(data);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "frames", fileName);
        }

        using var response = await apiClient.HttpClient.PostAsync(
            $"api/interview/sessions/{sessionId}/verify/face", content, cancellationToken);
        return await ReadResultAsync(response, cancellationToken);
    }

    public async Task<(bool Success, VerificationResultModel? Result, string? Error)> VerifyVoiceAsync(
        Guid sessionId, byte[] audio, string fileName, string contentType,
        CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(audio);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);

        using var response = await apiClient.HttpClient.PostAsync(
            $"api/interview/sessions/{sessionId}/verify/voice", content, cancellationToken);
        return await ReadResultAsync(response, cancellationToken);
    }

    private static async Task<(bool Success, VerificationResultModel? Result, string? Error)> ReadResultAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync<VerificationResultModel>(response, cancellationToken);
        if (!response.IsSuccessStatusCode || envelope is null || !envelope.Success || envelope.Data is null)
        {
            // 422 carries the ai-service quality reason (e.g. "Not enough usable frames") in Message.
            return (false, null, envelope?.Message ?? $"Verification failed ({(int)response.StatusCode}).");
        }

        return (true, envelope.Data, null);
    }

    private static async Task<ApiResponse<T>?> ReadEnvelopeAsync<T>(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions, cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}

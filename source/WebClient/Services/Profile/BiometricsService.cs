using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WebClient.Models.Profile;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.Profile;

public interface IBiometricsService
{
    Task<(bool Success, BiometricStatusModel? Status, string? Error)> GetStatusAsync(
        CancellationToken cancellationToken);

    // modality: "face" | "voice" — maps to POST api/profiles/me/biometrics/{modality}.
    Task<(bool Success, string? Error)> EnrollAsync(
        string modality, byte[] data, string fileName, string contentType,
        CancellationToken cancellationToken);
}

public sealed class BiometricsService(IApiClient apiClient) : IBiometricsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<(bool Success, BiometricStatusModel? Status, string? Error)> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        using var response = await apiClient.HttpClient.GetAsync(
            "api/profiles/me/biometrics", cancellationToken);
        var envelope = await ReadEnvelopeAsync<BiometricStatusModel>(response, cancellationToken);
        if (!response.IsSuccessStatusCode || envelope is null || !envelope.Success || envelope.Data is null)
        {
            return (false, null, envelope?.Message ?? $"Could not load enrollment status ({(int)response.StatusCode}).");
        }

        return (true, envelope.Data, null);
    }

    public async Task<(bool Success, string? Error)> EnrollAsync(
        string modality, byte[] data, string fileName, string contentType,
        CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(data);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);

        using var response = await apiClient.HttpClient.PostAsync(
            $"api/profiles/me/biometrics/{modality}", content, cancellationToken);
        var envelope = await ReadEnvelopeAsync<object>(response, cancellationToken);
        if (!response.IsSuccessStatusCode || envelope is null || !envelope.Success)
        {
            // 422 carries the ai-service quality reason (e.g. "No face detected") in Message.
            return (false, envelope?.Message ?? $"Enrollment failed ({(int)response.StatusCode}).");
        }

        return (true, null);
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

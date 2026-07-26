using System.Net;
using System.Text.Json;
using FeedbackAPI.DTOs;

namespace FeedbackAPI.Clients;

public class InterviewApiClient : IInterviewApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public InterviewApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SystemFeedbackEligibilityResult> GetSystemFeedbackEligibilityAsync(
        string? bearerToken,
        CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/interview/sessions/feedback-eligibility", bearerToken);
        using var response = await SendAsync(request, ct);
        var result = await ReadEnvelopeAsync<SystemFeedbackEligibilityResult>(response, ct);
        return result;
    }

    public async Task<SessionFeedbackEligibilityResult> GetSessionFeedbackEligibilityAsync(
        Guid sessionId,
        string? bearerToken,
        CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/interview/sessions/{sessionId}/feedback-eligibility", bearerToken);
        using var response = await SendAsync(request, ct);
        var result = await ReadEnvelopeAsync<SessionFeedbackEligibilityResult>(response, ct);
        if (result.SessionId != sessionId || string.IsNullOrWhiteSpace(result.Status))
            throw new InterviewApiDependencyException();
        return result;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string route, string? bearerToken)
    {
        var request = new HttpRequestMessage(method, route);
        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.Headers.TryAddWithoutValidation("Authorization", bearerToken);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await _httpClient.SendAsync(request, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            throw new InterviewApiTimeoutException();
        }
        catch (TimeoutException)
        {
            throw new InterviewApiTimeoutException();
        }
        catch (HttpRequestException)
        {
            throw new InterviewApiDependencyException();
        }
    }

    private static async Task<T> ReadEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InterviewApiUnauthorizedException();
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new InterviewApiForbiddenException();
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InterviewApiNotFoundException();
        if ((int)response.StatusCode >= 500 || !response.IsSuccessStatusCode)
            throw new InterviewApiDependencyException();

        try
        {
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions, ct);
            if (envelope?.Success != true || envelope.Data is null)
                throw new InterviewApiDependencyException();
            return envelope.Data;
        }
        catch (JsonException)
        {
            throw new InterviewApiDependencyException();
        }
    }
}

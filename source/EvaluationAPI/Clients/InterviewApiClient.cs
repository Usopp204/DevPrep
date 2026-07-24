using System.Net;
using System.Text.Json;
using EvaluationAPI.DTOs;

namespace EvaluationAPI.Clients;

public class InterviewApiClient : IInterviewApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<InterviewApiClient> _logger;

    public InterviewApiClient(HttpClient httpClient, ILogger<InterviewApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> OwnsSessionAsync(Guid sessionId, string? bearerToken, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"api/interview/sessions/{sessionId}/owns");
            if (!string.IsNullOrEmpty(bearerToken))
                request.Headers.TryAddWithoutValidation("Authorization", bearerToken);

            using var response = await _httpClient.SendAsync(request, ct);
            // 200 = owns; 404 (not owner / not found) / 401 / 403 = does not own.
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            // On a transient failure, deny access (fail closed) rather than leak data.
            _logger.LogWarning(ex,
                "Ownership check against InterviewAPI failed for session {SessionId}; denying access.",
                sessionId);
            return false;
        }
    }

    public async Task<SessionFeedbackEligibilityResult> GetSessionFeedbackEligibilityAsync(
        Guid sessionId,
        string? bearerToken,
        CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/interview/sessions/{sessionId}/feedback-eligibility");

            if (!string.IsNullOrWhiteSpace(bearerToken))
                request.Headers.TryAddWithoutValidation("Authorization", bearerToken);

            using var response = await _httpClient.SendAsync(request, ct);
            return await MapFeedbackEligibilityResponseAsync(sessionId, response, ct);
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
        catch (JsonException)
        {
            throw new InterviewApiDependencyException();
        }
    }


    private static async Task<SessionFeedbackEligibilityResult> MapFeedbackEligibilityResponseAsync(
        Guid expectedSessionId,
        HttpResponseMessage response,
        CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InterviewApiUnauthorizedException();

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new InterviewApiForbiddenException();

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InterviewApiSessionNotFoundException();

        if ((int)response.StatusCode >= 500)
            throw new InterviewApiDependencyException();

        if (!response.IsSuccessStatusCode)
            throw new InterviewApiDependencyException();

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<SessionFeedbackEligibilityResult>>(JsonOptions, ct);
        if (envelope?.Success != true || envelope.Data is null)
            throw new InterviewApiDependencyException();

        if (envelope.Data.SessionId != expectedSessionId || string.IsNullOrWhiteSpace(envelope.Data.Status))
            throw new InterviewApiDependencyException();

        return envelope.Data;
    }
}
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
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new InterviewApiUnauthorizedException();

            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                return false;

            if (response.StatusCode == HttpStatusCode.RequestTimeout
                || (int)response.StatusCode == 429
                || (int)response.StatusCode >= 500)
            {
                throw LogOwnershipDependencyFailure(sessionId, response.StatusCode);
            }

            if (!response.IsSuccessStatusCode)
                throw LogOwnershipDependencyFailure(sessionId, response.StatusCode);

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(JsonOptions, ct);
            if (envelope?.Success != true
                || envelope.Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                throw LogOwnershipDependencyFailure(sessionId, response.StatusCode);
            }

            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InterviewApiException)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning(
                "Ownership verification timed out for session {SessionId}; dependency status is unavailable.",
                sessionId);
            throw new InterviewApiTimeoutException();
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "Ownership verification timed out for session {SessionId}; dependency status is unavailable.",
                sessionId);
            throw new InterviewApiTimeoutException();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Ownership verification dependency failed for session {SessionId}; ownership was not inferred.",
                sessionId);
            throw new InterviewApiDependencyException();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Ownership verification returned an invalid response for session {SessionId}; ownership was not inferred.",
                sessionId);
            throw new InterviewApiDependencyException();
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(
                ex,
                "Ownership verification returned an unsupported response for session {SessionId}; ownership was not inferred.",
                sessionId);
            throw new InterviewApiDependencyException();
        }
    }

    private InterviewApiDependencyException LogOwnershipDependencyFailure(
        Guid sessionId,
        HttpStatusCode statusCode)
    {
        _logger.LogWarning(
            "Ownership verification dependency failed for session {SessionId} with status {StatusCode}; " +
            "ownership was not inferred.",
            sessionId,
            (int)statusCode);
        return new InterviewApiDependencyException();
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

    public async Task<SessionEvaluationContextResult> GetSessionEvaluationContextAsync(
        Guid sessionId,
        string? bearerToken,
        CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/interview/sessions/{sessionId}/transcript");

            if (!string.IsNullOrWhiteSpace(bearerToken))
                request.Headers.TryAddWithoutValidation("Authorization", bearerToken);

            using var response = await _httpClient.SendAsync(request, ct);
            return await MapSessionEvaluationContextResponseAsync(sessionId, response, ct);
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
    private static async Task<SessionEvaluationContextResult> MapSessionEvaluationContextResponseAsync(
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

        if (!response.IsSuccessStatusCode)
            throw new InterviewApiDependencyException();

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<SessionEvaluationContextResult>>(
            JsonOptions,
            ct);
        if (envelope?.Success != true
            || envelope.Data is null
            || envelope.Data.SessionId != expectedSessionId
            || string.IsNullOrWhiteSpace(envelope.Data.Status))
        {
            throw new InterviewApiDependencyException();
        }

        return envelope.Data;
    }
}
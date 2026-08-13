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

    public async Task RequestRegradeAsync(
        Guid sessionId,
        IReadOnlyCollection<Guid> missingSessionQuestionIds,
        string? bearerToken,
        CancellationToken ct = default)
    {
        // Defence in depth: the caller already proves this is non-empty (EvaluationService only
        // reaches the regrade when substantiveQuestionIds is a proper superset of the evaluated
        // ids). Re-checked here because an empty request would still cost InterviewAPI's
        // per-session cooldown, which would then suppress a REAL regrade moments later.
        if (missingSessionQuestionIds.Count == 0)
            return;

        // SHARED CIRCUIT BREAKER — read before assuming this call is isolated.
        // This method goes through the same typed HttpClient as OwnsSessionAsync and
        // GetSessionEvaluationContextAsync, so it shares ONE breaker instance with them
        // (Program.cs attaches ResiliencePolicies.InterviewApi to the client, not per-method).
        // Consequence: a persistent 5xx bug in InterviewAPI's /regrade-missing endpoint would
        // count toward that breaker and, after 5 consecutive failures, open it for ownership
        // checks and transcript fetches too — i.e. a recovery feature could stop reports being
        // viewed at all. Accepted deliberately: the 60s per-session cooldown makes five
        // consecutive regrade failures hard to reach unless InterviewAPI is genuinely unwell, and
        // in that case opening the breaker is the correct outcome. Note 4xx does NOT count
        // (HandleTransientHttpError only handles 5xx/408/connection errors), so an ordinary
        // rejected regrade is harmless. The isolation option — a separate named HttpClient with
        // its own policy — is recorded as C-03 in docs/architecture/code-followups.md.
        //
        // ONE catch-all around everything, on purpose. This method's contract is that it never
        // throws: the caller is about to answer the user's request and a failed recovery attempt
        // must not change that answer. There is no rethrow path anywhere below — if you add a
        // typed catch here, it must still swallow.
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"api/interview/sessions/{sessionId}/regrade-missing")
            {
                Content = JsonContent.Create(new { sessionQuestionIds = missingSessionQuestionIds })
            };
            if (!string.IsNullOrEmpty(bearerToken))
                request.Headers.TryAddWithoutValidation("Authorization", bearerToken);

            using var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Re-grade request for session {SessionId} was rejected with {StatusCode}; " +
                    "{MissingCount} answer(s) stay ungraded until the next report request.",
                    sessionId, (int)response.StatusCode, missingSessionQuestionIds.Count);
                return;
            }

            // Distinguish the three server-side outcomes in the log. A cooldown suppression is
            // EXPECTED and healthy (the user refreshed inside the 60s window and a burst is
            // already in flight); a drop or a rejection is not. Logging them at the same level
            // would make a real failure look like routine throttling.
            var envelope = await response.Content
                .ReadFromJsonAsync<ApiResponse<RegradeAcceptedResult>>(JsonOptions, ct);
            var result = envelope?.Data;

            if (result is null)
            {
                _logger.LogWarning(
                    "Re-grade request for session {SessionId} returned an unreadable body; " +
                    "cannot confirm whether grading resumed.",
                    sessionId);
            }
            else if (result.CooldownActive)
            {
                _logger.LogInformation(
                    "Re-grade for session {SessionId} was suppressed by InterviewAPI's cooldown " +
                    "({SecondsRemaining}s remaining) — a recent burst is already in flight. Expected " +
                    "when the report page is refreshed repeatedly.",
                    sessionId, result.CooldownSecondsRemaining);
            }
            else if (result.DroppedCount > 0)
            {
                _logger.LogWarning(
                    "Re-grade for session {SessionId}: {EnqueuedCount} queued but {DroppedCount} dropped " +
                    "(grading queue full). Those answers stay ungraded until the next attempt.",
                    sessionId, result.EnqueuedCount, result.DroppedCount);
            }
            else
            {
                _logger.LogInformation(
                    "Re-grade for session {SessionId}: {EnqueuedCount} of {RequestedCount} missing " +
                    "evaluation(s) re-queued on InterviewAPI.",
                    sessionId, result.EnqueuedCount, result.RequestedCount);
            }
        }
        catch (Exception ex)
        {
            // Includes BrokenCircuitException once the 5.2 breaker has opened, timeouts, DNS and
            // connection failures. All mean the same thing: recovery did not happen this time and
            // will be attempted again on the next report request.
            _logger.LogWarning(
                ex,
                "Re-grade request for session {SessionId} could not be delivered; {MissingCount} " +
                "answer(s) stay ungraded until the next report request.",
                sessionId, missingSessionQuestionIds.Count);
        }
    }

    /// <summary>Shape of InterviewAPI's RequeueMissingEvaluationsResponse — only the fields this
    /// client logs. Duplicated rather than shared: the two services have no common contract
    /// project, and this is a diagnostic payload, not a business contract.</summary>
    private sealed class RegradeAcceptedResult
    {
        public int RequestedCount { get; set; }
        public int EnqueuedCount { get; set; }
        public int DroppedCount { get; set; }
        public bool CooldownActive { get; set; }
        public int CooldownSecondsRemaining { get; set; }
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
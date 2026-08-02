namespace EvaluationAPI.Clients;

public interface IInterviewApiClient
{
    // Returns false only for a confirmed inaccessible session. Authentication and dependency
    // failures are typed exceptions so callers can preserve 401/403/503 distinctions.
    Task<bool> OwnsSessionAsync(Guid sessionId, string? bearerToken, CancellationToken ct = default);

    Task<SessionFeedbackEligibilityResult> GetSessionFeedbackEligibilityAsync(
        Guid sessionId,
        string? bearerToken,
        CancellationToken ct = default);

    Task<SessionEvaluationContextResult> GetSessionEvaluationContextAsync(
        Guid sessionId,
        string? bearerToken,
        CancellationToken ct = default);
}
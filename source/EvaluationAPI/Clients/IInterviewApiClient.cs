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

    /// <summary>
    /// Asks InterviewAPI to re-queue grading for answers that have no evaluation. Recovery only:
    /// the grading queue is in-memory with no dead letter, so a job whose retries were exhausted is
    /// gone and the aggregate report would otherwise be blocked forever.
    ///
    /// FIRE AND FORGET — this method NEVER throws. Every failure (InterviewAPI down, open circuit,
    /// timeout, 4xx, malformed body) is swallowed and logged. Callers use it as a side effect
    /// alongside the response they were already going to send: a failed recovery attempt must
    /// never replace, mask, or delay the real answer to the user's request. It returns void for
    /// that reason — there is deliberately no result for a caller to branch on.
    /// </summary>
    Task RequestRegradeAsync(
        Guid sessionId,
        IReadOnlyCollection<Guid> missingSessionQuestionIds,
        string? bearerToken,
        CancellationToken ct = default);
}
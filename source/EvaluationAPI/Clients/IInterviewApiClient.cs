namespace EvaluationAPI.Clients;

public interface IInterviewApiClient
{
    // Returns true if the JWT user owns the session, by calling InterviewAPI GET
    // /api/interview/sessions/{id}/owns (the caller's bearer token is forwarded).
    Task<bool> OwnsSessionAsync(Guid sessionId, string? bearerToken, CancellationToken ct = default);

    Task<SessionFeedbackEligibilityResult> GetSessionFeedbackEligibilityAsync(
        Guid sessionId,
        string? bearerToken,
        CancellationToken ct = default);

}
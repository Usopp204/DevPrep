namespace FeedbackAPI.Clients;

public interface IInterviewApiClient
{
    Task<SystemFeedbackEligibilityResult> GetSystemFeedbackEligibilityAsync(string? bearerToken, CancellationToken ct = default);
    Task<SessionFeedbackEligibilityResult> GetSessionFeedbackEligibilityAsync(Guid sessionId, string? bearerToken, CancellationToken ct = default);
}

public class SystemFeedbackEligibilityResult
{
    public bool Eligible { get; set; }
    public int QualifyingSessionCount { get; set; }
}

public class SessionFeedbackEligibilityResult
{
    public Guid SessionId { get; set; }
    public bool Eligible { get; set; }
    public string Status { get; set; } = string.Empty;
}
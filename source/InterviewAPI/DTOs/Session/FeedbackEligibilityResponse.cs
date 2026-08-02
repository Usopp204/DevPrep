namespace InterviewAPI.DTOs.Session;

public class FeedbackEligibilityResponse
{
    public bool Eligible { get; set; }
    public int QualifyingSessionCount { get; set; }
}

public class SessionFeedbackEligibilityResponse
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TerminationReason { get; set; }
    public bool HasSubstantiveAnswer { get; set; }
    public bool CanSubmitInterviewFeedback { get; set; }
    public bool CanGeneratePerformanceReport { get; set; }

    // Backward-compatible alias for clients that have not yet moved to the explicit
    // Interview Feedback capability. Performance authorization must never use this field.
    public bool Eligible => CanSubmitInterviewFeedback;
}

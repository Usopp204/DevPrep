namespace InterviewAPI.Helpers;

public static class SessionCapabilityPolicy
{
    public const string CandidateCancelled = "CandidateCancelled";
    public const string IntegrityViolation = "IntegrityViolation";
    public const string TimeExpired = "TimeExpired";

    public static SessionCapabilities Evaluate(
        string status,
        string? terminationReason,
        bool hasSubstantiveAnswer)
    {
        var terminal = status is "Completed" or "Cancelled" or "Expired";
        var reportAllowed = status == "Completed" && hasSubstantiveAnswer
            || status == "Cancelled"
                && hasSubstantiveAnswer
                && (string.IsNullOrEmpty(terminationReason)
                    || terminationReason == CandidateCancelled);

        return new SessionCapabilities(reportAllowed, terminal);
    }
}

public readonly record struct SessionCapabilities(
    bool CanGeneratePerformanceReport,
    bool CanSubmitInterviewFeedback);
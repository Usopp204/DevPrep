namespace EvaluationAPI.Clients;

public class SessionFeedbackEligibilityResult
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TerminationReason { get; set; }
    public bool HasSubstantiveAnswer { get; set; }
    public bool CanSubmitInterviewFeedback { get; set; }
    public bool CanGeneratePerformanceReport { get; set; }
    public bool Eligible { get; set; }
}

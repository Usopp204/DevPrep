namespace EvaluationAPI.Clients;

public class SessionFeedbackEligibilityResult
{
    public Guid SessionId { get; set; }
    public bool Eligible { get; set; }
    public string Status { get; set; } = string.Empty;
}
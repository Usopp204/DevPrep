namespace WebClient.Models.Interview;

public class FeedbackEligibilityResponse
{
    public bool Eligible { get; set; }
    public int QualifyingSessionCount { get; set; }
}

public class SessionFeedbackEligibilityResponse
{
    public Guid SessionId { get; set; }
    public bool Eligible { get; set; }
    public string Status { get; set; } = string.Empty;
}
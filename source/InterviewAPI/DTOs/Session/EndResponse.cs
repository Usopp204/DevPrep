namespace InterviewAPI.DTOs.Session;

public class EndResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime? EndedAt { get; set; }
    public string? TerminationReason { get; set; }
    public bool CanGeneratePerformanceReport { get; set; }
    public bool CanSubmitInterviewFeedback { get; set; }
    public bool ShouldEndSession { get; set; } = true;
    public string Message { get; set; } = string.Empty;
}
using WebClient.Models.Evaluation;
using WebClient.Models.Feedback;

namespace WebClient.Models.Interview;

public class SessionDetailViewModel
{
    public Guid SessionId { get; set; }
    public SessionHistoryItemDto? Session { get; set; }
    public SessionChatLogResponse? ChatLog { get; set; }
    public PerformanceReportResponse? PerformanceReport { get; set; }
    public SessionFeedbackEligibilityResponse? FeedbackEligibility { get; set; }
    public SubmitInterviewFeedbackRequest FeedbackForm { get; set; } = new();
    public MyInterviewFeedbackDto? MyInterviewFeedback { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ReportMessage { get; set; }
    public string? FeedbackMessage { get; set; }
    public bool FeedbackSubmitted { get; set; }
    public string? FeedbackValidationMessage { get; set; }
    public bool FeedbackFormWasSubmitted { get; set; }
    public InterviewSidebarViewModel Sidebar { get; set; } = new();

    public string Status => Session?.Status ?? ChatLog?.SessionStatus ?? FeedbackEligibility?.Status ?? string.Empty;
    public bool IsCompleted => string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase);
    public bool IsCancelled => string.Equals(Status, "Cancelled", StringComparison.OrdinalIgnoreCase);
    public bool IsExpired => string.Equals(Status, "Expired", StringComparison.OrdinalIgnoreCase);
    public bool CanSubmitInterviewFeedback => IsCompleted && FeedbackEligibility?.Eligible == true;
}

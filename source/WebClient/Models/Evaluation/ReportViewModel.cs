using WebClient.Models.Feedback;

namespace WebClient.Models.Evaluation;

public class ReportViewModel
{
    public Guid SessionId { get; set; }
    public string? Position { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public ReportResponse? Report { get; set; }
    public List<ReportQuestionViewModel> Questions { get; set; } = new();
    public SubmitInterviewFeedbackRequest FeedbackForm { get; set; } = new();
    public MyInterviewFeedbackDto? MyInterviewFeedback { get; set; }
    public bool CanSubmitInterviewFeedback { get; set; }
    public string? FeedbackMessage { get; set; }
    public string? FeedbackValidationMessage { get; set; }
    public bool FeedbackFormWasSubmitted { get; set; }
    public string? EmptyReason { get; set; }
}

public class ReportQuestionViewModel
{
    public int OrderIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    public string? AiFeedback { get; set; }
    public string? Strengths { get; set; }
}

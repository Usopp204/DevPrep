using WebClient.Models.Feedback;

namespace WebClient.Models.Evaluation;

public enum EvaluationReportState
{
    Scored,
    NoEvaluatedAnswers,
    ExpiredNoResult,
    EvaluationUnavailable,
    EvaluationIncomplete
}

public class ReportViewModel
{
    public Guid SessionId { get; set; }
    public int DurationActualSec { get; set; }
    public string? Position { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TerminationReason { get; set; }
    public EvaluationReportState DisplayState { get; set; }
    public string StateTitle { get; set; } = string.Empty;
    public string StateMessage { get; set; } = string.Empty;
    public bool QuestionEvaluationsUnavailable { get; set; }
    public ReportResponse? Report { get; set; }
    public List<ReportQuestionViewModel> Questions { get; set; } = new();
    public SubmitInterviewFeedbackRequest FeedbackForm { get; set; } = new();
    public MyInterviewFeedbackDto? MyInterviewFeedback { get; set; }
    public bool CanSubmitInterviewFeedback { get; set; }
    public string? FeedbackMessage { get; set; }
    public string? FeedbackValidationMessage { get; set; }
    public bool FeedbackFormWasSubmitted { get; set; }
    public UpdateInterviewFeedbackRequest EditFeedbackForm { get; set; } = new();
    public Guid? EditingFeedbackId { get; set; }
    public bool FeedbackEditFormWasSubmitted { get; set; }
}

public class ReportQuestionViewModel
{
    public Guid SessionQuestionId { get; set; }
    public int OrderIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public bool HasSubstantiveAnswer { get; set; }
    public decimal? Score { get; set; }
    public List<QuestionCriterionScoreViewModel> CriteriaScores { get; set; } = new();
    public string? AiFeedback { get; set; }
    public string? Strengths { get; set; }
    public string? AreasToImprove { get; set; }
}

public class QuestionCriterionScoreViewModel
{
    public string Label { get; set; } = string.Empty;
    public decimal Score { get; set; }
}

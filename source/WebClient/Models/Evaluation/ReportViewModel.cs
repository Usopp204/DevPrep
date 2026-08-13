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

    // True when DisplayState is EvaluationUnavailable but grading has actually been RESTARTED
    // (EvaluationAPI found zero evaluations and asked InterviewAPI to re-queue them). Exists
    // because one enum value now covers two very different situations: this recoverable one, and
    // the seven terminal bail-outs in EvaluationService that keep the "could not be evaluated"
    // wording. The view needs to tell them apart to pick an icon, and sniffing StateTitle for
    // display copy would be worse. A separate enum member was considered and rejected — it would
    // propagate through the enum, the state mapping and every branch in Report.cshtml for what is
    // a single call site's distinction.
    public bool RecoveryInProgress { get; set; }
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

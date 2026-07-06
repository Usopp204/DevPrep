using WebClient.Models.Evaluation;

namespace WebClient.Models.Interview;

// Backs Views/Interview/History.cshtml — session list (left) + transcript/score detail (right)
// for whichever session is selected.
public class HistoryViewModel
{
    public List<SessionListItemDto> Sessions { get; set; } = new();
    public Guid? SelectedSessionId { get; set; }
    public HistorySessionDetailViewModel? Detail { get; set; }
    public string? ErrorMessage { get; set; }
}

public class HistorySessionDetailViewModel
{
    public Guid SessionId { get; set; }
    public string? Position { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public int AnsweredQuestions { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public List<TranscriptQuestionDto> Questions { get; set; } = new();
    public List<ChatHistoryItem> ChatHistory { get; set; } = new();

    // Read-only: unlike the Report page, History never builds a report — it only ever
    // shows one if it already exists (GET /report, no POST).
    public ReportResponse? Report { get; set; }
    public List<EvaluateAnswerResponse> Evaluations { get; set; } = new();
}

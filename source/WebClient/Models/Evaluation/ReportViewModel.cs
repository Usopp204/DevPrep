namespace WebClient.Models.Evaluation;

// Backs Views/Evaluation/Report.cshtml. Combines the aggregate report (may not exist yet),
// per-question evaluations, and session context (position/level/duration from the transcript).
public class ReportViewModel
{
    public Guid SessionId { get; set; }
    public string? Position { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public ReportResponse? Report { get; set; }
    public List<ReportQuestionViewModel> Questions { get; set; } = new();

    // Set when the aggregate report could not be built yet (e.g. no answers were graded) —
    // the page still renders the transcript-derived info instead of failing outright.
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

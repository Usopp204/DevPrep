namespace EvaluationAPI.DTOs.Evaluation;

public static class EvaluationLifecycleStates
{
    public const string Scored = "Scored";
    public const string EvaluationUnavailable = "EvaluationUnavailable";
    public const string EvaluationIncomplete = "EvaluationIncomplete";
    public const string NoEvaluatedAnswers = "NoEvaluatedAnswers";
    public const string ExpiredNoResult = "ExpiredNoResult";
}

public class PerformanceReportResponse
{
    public string State { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public bool HasPersistedReport { get; set; }
    public decimal? OverallScore { get; set; }
    public decimal? TechnicalScore { get; set; }
    public decimal? CommunicationScore { get; set; }
    public decimal? ProblemSolvingScore { get; set; }
    public string? Summary { get; set; }
    public List<string> Strengths { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public string? Recommendations { get; set; }
    public string? Grade { get; set; }
}
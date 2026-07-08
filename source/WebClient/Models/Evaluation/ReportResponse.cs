namespace WebClient.Models.Evaluation;

// Mirrors EvaluationAPI.DTOs.Evaluation.ReportResponse.
public class ReportResponse
{
    public Guid ReportId { get; set; }
    public Guid SessionId { get; set; }
    public decimal OverallScore { get; set; }
    public decimal? TechnicalScore { get; set; }
    public decimal? TheoryScore { get; set; }
    public decimal? CommunicationScore { get; set; }
    public decimal? ProblemSolvingScore { get; set; }
    public string? Summary { get; set; }
    public string? Strengths { get; set; }
    public string? Improvements { get; set; }
    public string? Recommendations { get; set; }
    public string? Grade { get; set; }
    public DateTime GeneratedAt { get; set; }
}

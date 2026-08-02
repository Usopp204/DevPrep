namespace WebClient.Models.Evaluation;

// Mirrors EvaluationAPI.DTOs.Evaluation.ReportResponse.
public class ReportResponse
{
    public Guid SessionId { get; set; }
    public decimal OverallScore { get; set; }
    public decimal? TechnicalScore { get; set; }
    public decimal? CommunicationScore { get; set; }
    public decimal? ProblemSolvingScore { get; set; }
    public string? Summary { get; set; }
    public List<string> Strengths { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public string? Recommendations { get; set; }
    public string? Grade { get; set; }
}

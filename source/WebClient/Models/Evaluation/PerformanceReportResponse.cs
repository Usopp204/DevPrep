namespace WebClient.Models.Evaluation;

public class PerformanceReportResponse
{
    public Guid SessionId { get; set; }
    public decimal OverallScore { get; set; }
    public decimal? TechnicalScore { get; set; }
    public decimal? CommunicationScore { get; set; }
    public decimal? ProblemSolvingScore { get; set; }
    public string? Summary { get; set; }
    public List<string> Strengths { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public string? Grade { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<SkillBreakdownItemDto> SkillBreakdown { get; set; } = new();
}

public class SkillBreakdownItemDto
{
    public string Name { get; set; } = string.Empty;
    public decimal? Score { get; set; }
}
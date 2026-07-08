namespace CvJdAPI.Models;

public class MatchingScore
{
    public Guid MatchingScoreId { get; set; }
    public Guid CvId { get; set; }
    public Guid JdId { get; set; }
    public Guid UserId { get; set; }
    public decimal Score { get; set; }
    public string? MatchedKeywords { get; set; }
    public string? MissingKeywords { get; set; }
    public string? SuggestionText { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

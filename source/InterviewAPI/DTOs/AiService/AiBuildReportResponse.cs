using System.Text.Json.Serialization;

namespace InterviewAPI.DTOs.AiService;

public class AiBuildReportResponse
{
    [JsonPropertyName("overall_score")]
    public double OverallScore { get; set; }

    [JsonPropertyName("technical_score")]
    public double TechnicalScore { get; set; }

    [JsonPropertyName("theory_score")]
    public double TheoryScore { get; set; }

    [JsonPropertyName("communication_score")]
    public double CommunicationScore { get; set; }

    [JsonPropertyName("problem_solving_score")]
    public double ProblemSolvingScore { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("strengths")]
    public string Strengths { get; set; } = string.Empty;

    [JsonPropertyName("improvements")]
    public string Improvements { get; set; } = string.Empty;

    [JsonPropertyName("recommendations")]
    public string Recommendations { get; set; } = string.Empty;

    [JsonPropertyName("grade")]
    public string Grade { get; set; } = string.Empty;
}

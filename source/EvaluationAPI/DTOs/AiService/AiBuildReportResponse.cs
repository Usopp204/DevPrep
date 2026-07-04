using System.Text.Json.Serialization;

namespace EvaluationAPI.DTOs.AiService;

public class AiBuildReportResponse
{
    [JsonPropertyName("overall_score")]
    public double OverallScore { get; set; }

    [JsonPropertyName("num_questions")]
    public int NumQuestions { get; set; }

    [JsonPropertyName("categories")]
    public List<AiCategoryScore> Categories { get; set; } = new();

    [JsonPropertyName("summary")]
    public AiReportSummary? Summary { get; set; }

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = string.Empty;
}

public class AiCategoryScore
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = string.Empty;
}

public class AiReportSummary
{
    [JsonPropertyName("strengths")]
    public List<string> Strengths { get; set; } = new();

    [JsonPropertyName("areas_to_improve")]
    public List<string> AreasToImprove { get; set; } = new();

    [JsonPropertyName("praise")]
    public string Praise { get; set; } = string.Empty;
}

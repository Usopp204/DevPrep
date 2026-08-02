using System.Text.Json.Serialization;

namespace EvaluationAPI.DTOs.AiService;

public sealed class AiBuildReportRequest
{
    [JsonPropertyName("duration_actual_sec")]
    public int DurationActualSec { get; set; }

    [JsonPropertyName("evaluations")]
    public List<AiAggregateEvaluationRow> Evaluations { get; set; } = new();
}

public sealed class AiAggregateEvaluationRow
{
    [JsonPropertyName("qid")]
    public string Qid { get; set; } = string.Empty;

    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("user_answer")]
    public string UserAnswer { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("criteria")]
    public Dictionary<string, double> Criteria { get; set; } = new();

    [JsonPropertyName("analysis")]
    public string Analysis { get; set; } = string.Empty;

    [JsonPropertyName("praise")]
    public string Praise { get; set; } = string.Empty;

    [JsonPropertyName("suggested_improvement")]
    public string SuggestedImprovement { get; set; } = string.Empty;
}

public sealed class AiBuildReportResponse
{
    [JsonPropertyName("overall_score")]
    public double OverallScore { get; set; }

    [JsonPropertyName("num_questions")]
    public int NumQuestions { get; set; }

    [JsonPropertyName("summary")]
    public AiBuildReportSummary? Summary { get; set; }
}

public sealed class AiBuildReportSummary
{
    [JsonPropertyName("strengths")]
    public List<string>? Strengths { get; set; }

    [JsonPropertyName("areas_to_improve")]
    public List<string>? AreasToImprove { get; set; }

    [JsonPropertyName("praise")]
    public string? Praise { get; set; }

    [JsonPropertyName("advice")]
    public string? Advice { get; set; }
}
using System.Text.Json.Serialization;

namespace EvaluationAPI.DTOs.AiService;

public class AiBuildReportRequest
{
    [JsonPropertyName("duration_actual_sec")]
    public int DurationActualSec { get; set; }

    [JsonPropertyName("evaluations")]
    public List<AiEvalRow> Evaluations { get; set; } = new();
}

public class AiEvalRow
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("criteria")]
    public Dictionary<string, double>? Criteria { get; set; }
}

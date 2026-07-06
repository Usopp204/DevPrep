using System.Text.Json.Serialization;

namespace EvaluationAPI.DTOs.AiService;

public class AiEvaluateAnswerResponse
{
    [JsonPropertyName("score")]
    public double? Score { get; set; }

    [JsonPropertyName("criteria")]
    public Dictionary<string, double>? Criteria { get; set; }

    [JsonPropertyName("analysis")]
    public string Analysis { get; set; } = string.Empty;

    [JsonPropertyName("suggested_improvement")]
    public string SuggestedImprovement { get; set; } = string.Empty;

    [JsonPropertyName("praise")]
    public string Praise { get; set; } = string.Empty;

    [JsonPropertyName("model_used")]
    public string ModelUsed { get; set; } = string.Empty;
}

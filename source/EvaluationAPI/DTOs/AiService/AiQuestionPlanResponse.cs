using System.Text.Json.Serialization;

namespace EvaluationAPI.DTOs.AiService;

public class AiQuestionPlanResponse
{
    [JsonPropertyName("questions")]
    public List<AiPlannedQuestion> Questions { get; set; } = new();
}

public class AiPlannedQuestion
{
    [JsonPropertyName("order_index")]
    public int OrderIndex { get; set; }

    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("reference_answer")]
    public string ReferenceAnswer { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = string.Empty;
}

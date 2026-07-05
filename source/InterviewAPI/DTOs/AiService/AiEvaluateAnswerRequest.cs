using System.Text.Json.Serialization;

namespace InterviewAPI.DTOs.AiService;

public class AiEvaluateAnswerRequest
{
    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("reference_answer")]
    public string ReferenceAnswer { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = string.Empty;

    [JsonPropertyName("user_answer")]
    public string UserAnswer { get; set; } = string.Empty;

    [JsonPropertyName("followups")]
    public List<object>? Followups { get; set; }
}

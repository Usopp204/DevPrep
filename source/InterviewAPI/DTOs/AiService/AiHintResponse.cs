using System.Text.Json.Serialization;

namespace InterviewAPI.DTOs.AiService;

// Response of ai-service POST /internal/hint.
public class AiHintResponse
{
    [JsonPropertyName("hint")]
    public string Hint { get; set; } = string.Empty;
}

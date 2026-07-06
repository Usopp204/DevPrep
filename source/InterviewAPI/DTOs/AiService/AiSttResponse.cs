using System.Text.Json.Serialization;

namespace InterviewAPI.DTOs.AiService;

// Response of ai-service POST /internal/stt.
public class AiSttResponse
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

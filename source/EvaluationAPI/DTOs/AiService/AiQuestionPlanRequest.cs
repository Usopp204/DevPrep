using System.Text.Json.Serialization;

namespace EvaluationAPI.DTOs.AiService;

public class AiQuestionPlanRequest
{
    [JsonPropertyName("position")]
    public string Position { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public string Level { get; set; } = string.Empty;

    [JsonPropertyName("focus_areas")]
    public List<string> FocusAreas { get; set; } = new();

    [JsonPropertyName("duration_min")]
    public int DurationMin { get; set; }

    [JsonPropertyName("cv_text")]
    public string? CvText { get; set; }

    [JsonPropertyName("jd_text")]
    public string? JdText { get; set; }
}

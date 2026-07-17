using System.Text.Json.Serialization;

namespace InterviewAPI.DTOs.AiService;

// Mirrors ai-service POST /internal/biometrics/face/verify-live JSON (snake_case).
public class AiFaceVerifyLiveResponse
{
    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    [JsonPropertyName("live")]
    public bool Live { get; set; }

    [JsonPropertyName("identity_score")]
    public double? IdentityScore { get; set; }

    [JsonPropertyName("threshold")]
    public double Threshold { get; set; }

    [JsonPropertyName("yaw_range")]
    public double YawRange { get; set; }

    [JsonPropertyName("moved_enough")]
    public bool? MovedEnough { get; set; }

    [JsonPropertyName("direction_ok")]
    public bool? DirectionOk { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

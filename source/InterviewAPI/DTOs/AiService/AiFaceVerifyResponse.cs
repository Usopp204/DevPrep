using System.Text.Json.Serialization;

namespace InterviewAPI.DTOs.AiService;

// Mirrors ai-service POST /internal/biometrics/verify JSON — single-frame identity match, no
// challenge/liveness. Used by the in-session cheating check (VerifyFaceAsync), NOT entry
// verification (that still goes through VerifyFaceLiveAsync/face-verify-live).
public class AiFaceVerifyResponse
{
    [JsonPropertyName("match")]
    public bool Match { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("threshold")]
    public double Threshold { get; set; }
}

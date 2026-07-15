using System.Text.Json.Serialization;

namespace InterviewAPI.DTOs.AiService;

// Mirrors ai-service POST /internal/biometrics/voice/verify-challenge JSON (snake_case).
public class AiVoiceVerifyChallengeResponse
{
    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    [JsonPropertyName("speaker_score")]
    public double SpeakerScore { get; set; }

    [JsonPropertyName("content_score")]
    public double ContentScore { get; set; }

    [JsonPropertyName("content_ok")]
    public bool ContentOk { get; set; }

    [JsonPropertyName("threshold")]
    public double Threshold { get; set; }

    [JsonPropertyName("transcript")]
    public string Transcript { get; set; } = string.Empty;
}

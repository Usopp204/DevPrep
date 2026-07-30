namespace InterviewAPI.DTOs.Biometrics;

// Returned after one microphone answer is transcribed and checked against the
// enrolled voice template. Only a confirmed speaker mismatch is a cheating strike.
public class VoiceAnswerVerifyResponse
{
    public string Text { get; set; } = string.Empty;
    public bool VoiceChecked { get; set; }
    public bool? VoiceMatched { get; set; }
    public double? Score { get; set; }
    public int StrikeCount { get; set; }
    public int MaxStrikes { get; set; }
    public bool ShouldEndSession { get; set; }
    public bool Duplicate { get; set; }
    public string? Message { get; set; }
}

namespace InterviewAPI.DTOs.Session;

// Text the client wants spoken by the AI interviewer (TTS).
public class TtsRequest
{
    public string Text { get; set; } = string.Empty;
}

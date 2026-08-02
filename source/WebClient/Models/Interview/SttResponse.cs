namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.SttResponse (transcribed voice answer).
public class SttResponse
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
    public string Status { get; set; } = string.Empty;
    public string? TerminationReason { get; set; }
    public bool CanGeneratePerformanceReport { get; set; }
    public bool CanSubmitInterviewFeedback { get; set; }
}

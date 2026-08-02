namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.MessageResponse.
public class MessageResponse
{
    public bool Ended { get; set; }
    public bool ShouldEndSession { get; set; }
    public string Message { get; set; } = string.Empty;
    public NextQuestionDto? NextQuestion { get; set; }
    public int AnsweredQuestions { get; set; }
    public int TotalQuestions { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TerminationReason { get; set; }
    public bool CanGeneratePerformanceReport { get; set; }
    public bool CanSubmitInterviewFeedback { get; set; }
}

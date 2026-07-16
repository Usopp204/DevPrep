namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.ResumeResponse.
public class ResumeResponse
{
    public string Status { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string Level { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public int RemainingSeconds { get; set; }
    public NextQuestionDto? CurrentQuestion { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredQuestions { get; set; }
    public int PauseCount { get; set; }
    public int PausesRemaining { get; set; }
    public int PausedSecondsRemaining { get; set; }
    public bool HasSeenRoomTour { get; set; }
    public List<ChatHistoryItem> ChatHistory { get; set; } = new();
}

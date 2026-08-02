namespace InterviewAPI.DTOs.Session;

public class TranscriptResponse
{
    public Guid SessionId { get; set; }
    public string? Position { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TerminationReason { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int TotalPausedSeconds { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredQuestions { get; set; }
    public List<TranscriptQuestionDto> Questions { get; set; } = new();
    public List<ChatHistoryItem> ChatHistory { get; set; } = new();
}

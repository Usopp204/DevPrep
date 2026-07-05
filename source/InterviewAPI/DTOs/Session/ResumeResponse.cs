namespace InterviewAPI.DTOs.Session;

public class ResumeResponse
{
    public string Status { get; set; } = string.Empty;
    // Session config, used by the Room UI to compose the static greeting line.
    public string? Position { get; set; }
    public string Level { get; set; } = string.Empty;
    public int RemainingSeconds { get; set; }
    public NextQuestionDto? CurrentQuestion { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredQuestions { get; set; }
    public List<ChatHistoryItem> ChatHistory { get; set; } = new();
}

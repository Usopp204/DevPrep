namespace InterviewAPI.DTOs.Session;

public class SessionHistoryItemDto
{
    public Guid SessionId { get; set; }
    public string? Title { get; set; }
    public string? Position { get; set; }
    public string Level { get; set; } = null!;
    public string InteractionMode { get; set; } = null!;
    public string QuestionType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredQuestions { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

namespace WebClient.Models.Interview;

public class SessionHistoryItemDto
{
    public Guid SessionId { get; set; }
    public string? Title { get; set; }
    public string? Position { get; set; }
    public string Level { get; set; } = string.Empty;
    public string InteractionMode { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredQuestions { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Title is never populated (the server column was removed) — Position is the real display
    // source.
    public string DisplayTitle => Position ?? "Interview Session";
    public bool IsCompleted => string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase);
    public bool IsCancelled => string.Equals(Status, "Cancelled", StringComparison.OrdinalIgnoreCase);
    public bool IsExpired => string.Equals(Status, "Expired", StringComparison.OrdinalIgnoreCase);
    public string DisplayStatus => Status;
}

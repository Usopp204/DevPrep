namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.SessionListItemDto.
public class SessionListItemDto
{
    public Guid SessionId { get; set; }
    public string? Title { get; set; }
    public string? Position { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredQuestions { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

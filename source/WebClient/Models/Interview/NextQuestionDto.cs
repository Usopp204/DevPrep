namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.NextQuestionDto (used by both Resume and Message responses).
public class NextQuestionDto
{
    public Guid SessionQuestionId { get; set; }
    public int OrderIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int IdleReminderSeconds { get; set; }
    public int IdleHardSeconds { get; set; }
    public int IdleElapsedSeconds { get; set; }
    public Guid? VerificationCheckId { get; set; }
    public DateTime? VerificationCheckExpiresAt { get; set; }
}

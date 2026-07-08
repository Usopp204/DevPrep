namespace InterviewAPI.DTOs.Session;

public class NextQuestionDto
{
    public Guid SessionQuestionId { get; set; }
    public int OrderIndex { get; set; }
    public string Content { get; set; } = string.Empty;

    // Idle-reminder/auto-advance thresholds for THIS question (seconds since AskedAt), plus how
    // much idle time has already elapsed (non-zero on resume/refresh) — see SessionService.
    public int IdleReminderSeconds { get; set; }
    public int IdleHardSeconds { get; set; }
    public int IdleElapsedSeconds { get; set; }
}

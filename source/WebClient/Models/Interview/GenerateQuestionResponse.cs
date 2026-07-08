namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.GenerateQuestionResponse.
public class GenerateQuestionResponse
{
    public Guid SessionQuestionId { get; set; }
    public int OrderIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public bool Ended { get; set; }
    public int IdleReminderSeconds { get; set; }
    public int IdleHardSeconds { get; set; }
    public int IdleElapsedSeconds { get; set; }
}

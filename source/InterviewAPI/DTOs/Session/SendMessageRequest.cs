namespace InterviewAPI.DTOs.Session;

public class SendMessageRequest
{
    public Guid SessionQuestionId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? ResponseTimeMs { get; set; }
}

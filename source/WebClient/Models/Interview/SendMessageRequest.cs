namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.SendMessageRequest.
public class SendMessageRequest
{
    public Guid SessionQuestionId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? ResponseTimeMs { get; set; }
}

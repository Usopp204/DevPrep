namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.ChatHistoryItem.
public class ChatHistoryItem
{
    public Guid MessageId { get; set; }
    public Guid SessionQuestionId { get; set; }
    public string SenderType { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public int? ResponseTimeMs { get; set; }
}

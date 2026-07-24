namespace WebClient.Models.Interview;

public class SessionChatLogResponse
{
    public Guid SessionId { get; set; }
    public string SessionStatus { get; set; } = string.Empty;
    public List<SessionChatLogQuestionDto> Questions { get; set; } = new();
}

public class SessionChatLogQuestionDto
{
    public int QuestionIndex { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public List<SessionChatLogMessageDto> Messages { get; set; } = new();
}

public class SessionChatLogMessageDto
{
    public Guid MessageId { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public int? ResponseDurationMs { get; set; }
}
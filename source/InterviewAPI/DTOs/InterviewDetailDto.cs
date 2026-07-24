namespace InterviewAPI.DTOs;

/// <summary>
/// Chi tiết một phiên phỏng vấn, kèm đoạn chat: danh sách câu hỏi (theo OrderIndex),
/// mỗi câu hỏi gồm các tin nhắn (câu hỏi của AI + câu trả lời của người dùng) theo thời gian.
/// </summary>
public class InterviewDetailDto
{
    public Guid SessionId { get; set; }

    public Guid UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserFullName { get; set; }

    public Guid? CvId { get; set; }
    public Guid? JdId { get; set; }
    public string Level { get; set; } = null!;
    public string InteractionMode { get; set; } = null!;
    public string QuestionType { get; set; } = null!;
    public string Status { get; set; } = null!;

    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredQuestions { get; set; }
    public int? AvgResponseTimeMs { get; set; }
    public int CreditCost { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<SessionQuestionDto> Questions { get; set; } = new();
}

/// <summary>Một câu hỏi trong phiên, kèm các tin nhắn chat thuộc câu hỏi đó.</summary>
public class SessionQuestionDto
{
    public Guid SessionQuestionId { get; set; }
    public int OrderIndex { get; set; }
    public string Content { get; set; } = null!;     // nội dung câu hỏi (AI sinh hoặc lấy từ ngân hàng)
    public bool IsAiGenerated { get; set; }
    public string? ContextSource { get; set; }
    public DateTime? AskedAt { get; set; }

    public List<ChatMessageDto> Messages { get; set; } = new();
}

/// <summary>Một tin nhắn trong đoạn chat (câu hỏi của AI / câu trả lời của người dùng / hệ thống).</summary>
public class ChatMessageDto
{
    public Guid MessageId { get; set; }
    public string SenderType { get; set; } = null!;   // User / AI / System
    public string MessageType { get; set; } = null!;  // Text / Question / Answer / Audio / System
    public string Content { get; set; } = null!;
    public string? AudioUrl { get; set; }
    public int? ResponseTimeMs { get; set; }
    public DateTime SentAt { get; set; }
}

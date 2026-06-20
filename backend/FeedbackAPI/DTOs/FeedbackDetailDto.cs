namespace FeedbackAPI.DTOs;

/// <summary>Chi tiết một feedback (kèm danh sách trả lời nếu là System feedback).</summary>
public class FeedbackDetailDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;   // "System" | "AI"
    public Guid UserId { get; set; }

    // System-only
    public string? Subject { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }

    // AI-only
    public Guid? SessionId { get; set; }
    public bool? IsHelpful { get; set; }

    // Common
    public string? Content { get; set; }
    public byte? Rating { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<FeedbackReplyDto> Replies { get; set; } = new();
}

/// <summary>Một câu trả lời của admin cho System feedback.</summary>
public class FeedbackReplyDto
{
    public Guid ReplyId { get; set; }
    public Guid RepliedBy { get; set; }
    public string ReplyContent { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

namespace FeedbackAPI.DTOs;

/// <summary>Một câu trả lời của admin cho System feedback.</summary>
public class FeedbackReplyDto
{
    public Guid ReplyId { get; set; }
    public Guid FeedbackId { get; set; }
    public Guid RepliedBy { get; set; }

    /// <summary>Tên admin trả lời — resolve từ UserAPI theo RepliedBy (null nếu không lấy được).</summary>
    public string? RepliedByName { get; set; }

    public string ReplyContent { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

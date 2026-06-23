namespace FeedbackAPI.DTOs;

/// <summary>Một câu trả lời của admin cho System feedback.</summary>
public class FeedbackReplyDto
{
    public Guid ReplyId { get; set; }
    public Guid FeedbackId { get; set; }
    public Guid RepliedBy { get; set; }
    public string ReplyContent { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

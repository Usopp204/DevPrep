namespace FeedbackAPI.DTOs;

/// <summary>Một dòng trong danh sách System feedback (góp ý/báo lỗi hệ thống — bảng SystemFeedbacks).</summary>
public class SystemFeedbackListItemDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Category { get; set; } = null!;
    public string? Subject { get; set; }
    public string? Content { get; set; }
    public byte? Rating { get; set; }
    public string Status { get; set; } = null!;
    public int ReplyCount { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<FeedbackReplyDto> Replies { get; set; } = new();
}

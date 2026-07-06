namespace FeedbackAPI.DTOs;

/// <summary>Một dòng trong danh sách System feedback (góp ý/báo lỗi hệ thống — bảng SystemFeedbacks).</summary>
public class SystemFeedbackListItemDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Tên người dùng — resolve từ UserAPI theo UserId (null nếu không lấy được).</summary>
    public string? UserFullName { get; set; }

    /// <summary>Email người dùng — resolve từ AuthAPI theo UserId (null nếu không lấy được).</summary>
    public string? UserEmail { get; set; }

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

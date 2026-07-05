namespace FeedbackAPI.DTOs;

/// <summary>Một dòng trong danh sách AI feedback (đánh giá buổi phỏng vấn AI — bảng InterviewFeedbacks).</summary>
public class AiFeedbackListItemDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Tên người dùng — resolve từ UserAPI theo UserId (null nếu không lấy được).</summary>
    public string? UserFullName { get; set; }

    /// <summary>Email người dùng — resolve từ AuthAPI theo UserId (null nếu không lấy được).</summary>
    public string? UserEmail { get; set; }

    public byte Rating { get; set; }
    public string? Content { get; set; }
    public bool? IsHelpful { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}

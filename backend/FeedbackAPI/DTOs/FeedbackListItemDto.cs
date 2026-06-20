namespace FeedbackAPI.DTOs;

/// <summary>
/// Một dòng trong danh sách feedback (gộp chung cả System và AI để hiển thị 1 list).
/// Các field chỉ áp dụng cho 1 loại sẽ null ở loại còn lại.
/// </summary>
public class FeedbackListItemDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;   // "System" | "AI"
    public Guid UserId { get; set; }

    // System-only
    public string? Subject { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }
    public int ReplyCount { get; set; }

    // AI-only
    public Guid? SessionId { get; set; }
    public bool? IsHelpful { get; set; }

    // Common
    public string? Content { get; set; }
    public byte? Rating { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}

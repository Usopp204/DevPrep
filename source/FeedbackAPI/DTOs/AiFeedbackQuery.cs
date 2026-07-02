namespace FeedbackAPI.DTOs;

/// <summary>
/// Tham số lọc/tìm kiếm danh sách AI feedback. Mọi field tuỳ chọn (bỏ trống = không lọc theo field đó).
/// Keyword tìm trong nội dung (Content).
/// </summary>
public class AiFeedbackQuery
{
    public string? Keyword { get; set; }
    public byte? Rating { get; set; }
    public bool? IsHelpful { get; set; }
    public Guid? UserId { get; set; }
    public Guid? SessionId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

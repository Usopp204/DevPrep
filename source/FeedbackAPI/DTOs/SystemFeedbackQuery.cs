namespace FeedbackAPI.DTOs;

/// <summary>
/// Tham số lọc/tìm kiếm danh sách System feedback. Mọi field tuỳ chọn (bỏ trống = không lọc theo field đó).
/// Keyword tìm trong Subject + Content.
/// </summary>
public class SystemFeedbackQuery
{
    public string? Keyword { get; set; }
    public string? Category { get; set; }   // General / Bug / FeatureRequest / UI / Performance / Other
    public string? Status { get; set; }      // New / InReview / Replied / Resolved / Closed
    public byte? Rating { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    /// <summary>true = kèm cả bản ghi đã xoá mềm (cho admin xem/khôi phục).</summary>
    public bool IncludeDeleted { get; set; } = false;

    /// <summary>true = CHỈ lấy bản ghi đã xoá mềm (trang "Show deleted" của admin). Ưu tiên hơn IncludeDeleted.</summary>
    public bool OnlyDeleted { get; set; } = false;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

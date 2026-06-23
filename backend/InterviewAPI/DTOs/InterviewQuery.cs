namespace InterviewAPI.DTOs;

/// <summary>
/// Tham số lọc/tìm kiếm danh sách phiên phỏng vấn. Mọi field tuỳ chọn (bỏ trống = không lọc).
/// Keyword tìm trong Title.
/// </summary>
public class InterviewQuery
{
    public string? Keyword { get; set; }
    public string? Status { get; set; }          // Configured / Initialized / InProgress / Completed / Expired / Cancelled
    public string? Level { get; set; }           // Intern / Junior / Middle / Senior / Lead
    public string? InteractionMode { get; set; } // Text / Voice / Video
    public string? QuestionType { get; set; }    // Theory / Coding / Behavioral / Mixed
    public Guid? UserId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

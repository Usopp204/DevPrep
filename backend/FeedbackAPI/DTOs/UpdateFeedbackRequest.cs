namespace FeedbackAPI.DTOs;

/// <summary>
/// Dữ liệu cập nhật feedback (partial update: chỉ field nào gửi lên mới đổi).
/// System feedback áp dụng: Status, Subject, Category, Content, Rating.
/// AI feedback áp dụng: Content, Rating, IsHelpful.
/// </summary>
public class UpdateFeedbackRequest
{
    // System feedback
    public string? Status { get; set; }     // New / InReview / Replied / Resolved / Closed
    public string? Subject { get; set; }
    public string? Category { get; set; }   // General / Bug / FeatureRequest / UI / Performance / Other

    // Common
    public string? Content { get; set; }
    public byte? Rating { get; set; }        // 1..5

    // AI (interview) feedback
    public bool? IsHelpful { get; set; }
}

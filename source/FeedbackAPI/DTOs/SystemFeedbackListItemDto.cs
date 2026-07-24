namespace FeedbackAPI.DTOs;

public class SystemFeedbackListItemDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }
    public string Category { get; set; } = null!;
    public string? Subject { get; set; }
    public string? Content { get; set; }
    public byte? Rating { get; set; }
    public byte? UiUxRating { get; set; }
    public byte? UsabilityRating { get; set; }
    public byte? FeatureQualityRating { get; set; }
    public string Status { get; set; } = null!;
    public string ModerationStatus { get; set; } = null!;
    public bool IsVisible { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public Guid? ModeratedBy { get; set; }
    public int ReplyCount { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<FeedbackReplyDto> Replies { get; set; } = new();
}

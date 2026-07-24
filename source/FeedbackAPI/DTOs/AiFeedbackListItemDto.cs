namespace FeedbackAPI.DTOs;

public class AiFeedbackListItemDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }
    public byte Rating { get; set; }
    public byte? QuestionQualityRating { get; set; }
    public byte? AiAccuracyRating { get; set; }
    public byte? ScoringFairnessRating { get; set; }
    public byte? FeedbackUsefulnessRating { get; set; }
    public string? Content { get; set; }
    public bool? IsHelpful { get; set; }
    public string ModerationStatus { get; set; } = null!;
    public bool IsVisible { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public Guid? ModeratedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}

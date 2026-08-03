using System.ComponentModel.DataAnnotations;
using FeedbackAPI.Validation;

namespace FeedbackAPI.DTOs;

public class PublicFeedbackQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 5;
}

/// <summary>
/// One administrator answer shown underneath a public review. The identity of the
/// administrator who wrote it is deliberately left out: a reply is published on behalf of
/// the team, not of a named staff member.
/// </summary>
public class PublicFeedbackReplyDto
{
    public Guid ReplyId { get; set; }
    public string ReplyContent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class PublicSystemFeedbackDto
{
    public Guid FeedbackId { get; set; }
    public string AuthorDisplayName { get; set; } = PublicAuthorDisplayName.Fallback;
    public byte? OverallRating { get; set; }
    public byte? UiUxRating { get; set; }
    public byte? UsabilityRating { get; set; }
    public byte? FeatureQualityRating { get; set; }
    public string? Comment { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool CanReport { get; set; }
    public bool HasReported { get; set; }
    public bool IsOwner { get; set; }
    public List<PublicFeedbackReplyDto> Replies { get; set; } = [];
}

public class PublicAiFeedbackDto
{
    public Guid FeedbackId { get; set; }
    public string AuthorDisplayName { get; set; } = PublicAuthorDisplayName.Fallback;
    public byte OverallRating { get; set; }
    public byte? QuestionQualityRating { get; set; }
    public byte? AiAccuracyRating { get; set; }
    public byte? ScoringFairnessRating { get; set; }
    public byte? FeedbackUsefulnessRating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsOwner { get; set; }
}

public class MySystemFeedbackDto
{
    public Guid FeedbackId { get; set; }
    public byte OverallRating { get; set; }
    public byte UiUxRating { get; set; }
    public byte UsabilityRating { get; set; }
    public byte FeatureQualityRating { get; set; }
    public string? Comment { get; set; }
    public bool IsVisible { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MyInterviewFeedbackDto
{
    public Guid FeedbackId { get; set; }
    public Guid SessionId { get; set; }
    public byte OverallRating { get; set; }
    public byte? QuestionQualityRating { get; set; }
    public byte? AiAccuracyRating { get; set; }
    public byte? ScoringFairnessRating { get; set; }
    public byte? FeedbackUsefulnessRating { get; set; }
    public bool? IsHelpful { get; set; }
    public string? Comment { get; set; }

    public bool IsVisible { get; set; }
    public DateTime CreatedAt { get; set; }
}
public class SubmitSystemFeedbackRequest
{
    [Range(1, 5)]
    public int OverallRating { get; set; }

    [Range(1, 5)]
    public int UiUxRating { get; set; }

    [Range(1, 5)]
    public int UsabilityRating { get; set; }

    [Range(1, 5)]
    public int FeatureQualityRating { get; set; }

    [StringLength(SystemFeedbackRules.MaxCommentLength, ErrorMessage = "Comment cannot exceed 4000 characters.")]
    public string? Comment { get; set; }
}

public class SystemFeedbackSubmissionStatusDto
{
    public bool HasSubmittedSystemFeedback { get; set; }
    public int FeedbackCount { get; set; }
    public DateTime? LastSubmittedAt { get; set; }
    public Guid? ActiveFeedbackId { get; set; }
}

public class SubmitInterviewFeedbackRequest
{
    [Required]
    public Guid SessionId { get; set; }

    [Range(1, 5)]
    public int OverallRating { get; set; }

    [Range(1, 5)]
    public int QuestionQualityRating { get; set; }

    [Range(1, 5)]
    public int AiAccuracyRating { get; set; }

    [Range(1, 5)]
    public int ScoringFairnessRating { get; set; }

    [Range(1, 5)]
    public int FeedbackUsefulnessRating { get; set; }

    /// <summary>
    /// Optional thumbs up/down on the session as a whole. Left null when the candidate skips
    /// it, which is why InterviewFeedback.IsHelpful is nullable and why the administration
    /// list offers Helpful / Not helpful as a filter rather than a required column.
    /// </summary>
    public bool? IsHelpful { get; set; }

    [StringLength(InterviewFeedbackRules.MaxCommentLength, ErrorMessage = "Comment cannot exceed 1000 characters.")]
    public string? Comment { get; set; }
}

public class UpdateSystemFeedbackRequest
{
    [Range(1, 5)] public int OverallRating { get; set; }
    [Range(1, 5)] public int UiUxRating { get; set; }
    [Range(1, 5)] public int UsabilityRating { get; set; }
    [Range(1, 5)] public int FeatureQualityRating { get; set; }
    [StringLength(SystemFeedbackRules.MaxCommentLength, ErrorMessage = "Comment cannot exceed 4000 characters.")]
    public string? Comment { get; set; }
}

public class UpdateInterviewFeedbackRequest
{
    [Range(1, 5)] public int OverallRating { get; set; }
    [Range(1, 5)] public int QuestionQualityRating { get; set; }
    [Range(1, 5)] public int AiAccuracyRating { get; set; }
    [Range(1, 5)] public int ScoringFairnessRating { get; set; }
    [Range(1, 5)] public int FeedbackUsefulnessRating { get; set; }
    [StringLength(InterviewFeedbackRules.MaxCommentLength, ErrorMessage = "Comment cannot exceed 1000 characters.")]
    public string? Comment { get; set; }
}

public class CreatedSystemFeedbackDto
{
    public Guid FeedbackId { get; set; }
    public byte OverallRating { get; set; }
    public byte UiUxRating { get; set; }
    public byte UsabilityRating { get; set; }
    public byte FeatureQualityRating { get; set; }
    public string? Comment { get; set; }

    public bool IsVisible { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatedInterviewFeedbackDto
{
    public Guid FeedbackId { get; set; }
    public Guid SessionId { get; set; }
    public byte OverallRating { get; set; }
    public byte QuestionQualityRating { get; set; }
    public byte AiAccuracyRating { get; set; }
    public byte ScoringFairnessRating { get; set; }
    public byte FeedbackUsefulnessRating { get; set; }
    public string? Comment { get; set; }

    public bool IsVisible { get; set; }
    public DateTime CreatedAt { get; set; }
}

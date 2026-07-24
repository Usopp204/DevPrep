using System.ComponentModel.DataAnnotations;
using FeedbackAPI.Validation;

namespace FeedbackAPI.DTOs;

public class CreateFeedbackReportRequest
{
    [Required]
    [StringLength(50)]
    public string ReasonCode { get; set; } = string.Empty;

    [StringLength(FeedbackReportInput.MaxDetailsLength,
        ErrorMessage = "Details cannot exceed 1000 characters.")]
    public string? Details { get; set; }
}

public class CreatedFeedbackReportDto
{
    public Guid ReportId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class FeedbackReportQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? FeedbackType { get; set; }
    public string? Status { get; set; } = FeedbackReportStatus.Pending;
    public string? ReasonCode { get; set; }
    public string? Keyword { get; set; }
}

public class FeedbackReportListItemDto
{
    public Guid ReportId { get; set; }
    public string FeedbackType { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string ReasonLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FeedbackContent { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
    public int RelatedReportCount { get; set; }
    public bool IsFeedbackVisible { get; set; }
    public bool IsFeedbackDeleted { get; set; }
}

public class RelatedFeedbackReportDto
{
    public Guid ReportId { get; set; }
    public Guid ReporterUserId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string ReasonLabel { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ResolutionNote { get; set; }
}

public class FeedbackReportDetailsDto : FeedbackReportListItemDto
{
    public Guid ReporterUserId { get; set; }
    public byte? OverallRating { get; set; }
    public byte? QuestionQualityRating { get; set; }
    public byte? AiAccuracyRating { get; set; }
    public byte? ScoringFairnessRating { get; set; }
    public byte? FeedbackUsefulnessRating { get; set; }
    public byte? UiUxRating { get; set; }
    public byte? UsabilityRating { get; set; }
    public byte? FeatureQualityRating { get; set; }
    public string? FeedbackModerationStatus { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ResolutionNote { get; set; }
    public IReadOnlyList<RelatedFeedbackReportDto> RelatedReports { get; set; } = [];
}

public class ResolveFeedbackReportRequest
{
    [StringLength(FeedbackReportInput.MaxResolutionNoteLength,
        ErrorMessage = "Resolution note cannot exceed 1000 characters.")]
    public string? ResolutionNote { get; set; }
}

public class FeedbackReportResolutionDto
{
    public Guid ReportId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ResolutionNote { get; set; }
    public int ResolvedReportCount { get; set; }
    public bool FeedbackDeleted { get; set; }
}

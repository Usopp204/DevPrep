using System.ComponentModel.DataAnnotations;
using WebClient.Models.Shared;

namespace WebClient.Models.Feedback;

public static class FeedbackReportRules
{
    public const int MaxDetailsLength = 1000;
    public const int MaxResolutionNoteLength = 1000;
    public const string SystemType = "System";
    public const string InterviewType = "Interview";
    public const string PendingStatus = "Pending";
    public const string DismissedStatus = "Dismissed";
    public const string ResolvedStatus = "ResolvedByFeedbackDeletion";
    public const string OtherReason = "Other";

    public static readonly IReadOnlyDictionary<string, string> Reasons =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HarassmentOrPersonalAttack"] = "Harassment or personal attack",
            ["HateOrDiscrimination"] = "Hate or discrimination",
            ["ThreatsOrViolence"] = "Threats or violent content",
            ["SexualOrInappropriateContent"] = "Sexual or inappropriate content",
            ["SpamOrMisleading"] = "Spam, scam, or misleading information",
            ["PrivacyOrPersonalInformation"] = "Privacy or personal information",
            ["OffTopicOrIrrelevant"] = "Off-topic or irrelevant content",
            [OtherReason] = "Other"
        };
}

public class CreateFeedbackReportRequest
{
    [Required(ErrorMessage = "Choose a reason for reporting this feedback.")]
    public string ReasonCode { get; set; } = string.Empty;

    [StringLength(FeedbackReportRules.MaxDetailsLength,
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

    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ResolutionNote { get; set; }
    public IReadOnlyList<RelatedFeedbackReportDto> RelatedReports { get; set; } = [];
}

public class ResolveFeedbackReportRequest
{
    [StringLength(FeedbackReportRules.MaxResolutionNoteLength,
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

public class ReportedFeedbackPageViewModel
{
    public PagedResult<FeedbackReportListItemDto> Reports { get; set; } = new();
    public FeedbackReportDetailsDto? SelectedReport { get; set; }
    public string FeedbackType { get; set; } = "All";
    public string Status { get; set; } = "All";
    public string ReasonCode { get; set; } = "All";
    public string Keyword { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

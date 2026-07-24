using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Models;

[Index(nameof(Status), nameof(CreatedAt), Name = "IX_FeedbackReports_Status_CreatedAt")]
[Index(nameof(InterviewFeedbackId), Name = "IX_FeedbackReports_InterviewFeedbackId")]
[Index(nameof(SystemFeedbackId), Name = "IX_FeedbackReports_SystemFeedbackId")]
[Index(nameof(ReporterUserId), Name = "IX_FeedbackReports_ReporterUserId")]
[Index(nameof(ReasonCode), Name = "IX_FeedbackReports_ReasonCode")]
public class FeedbackReport
{
    [Key]
    public Guid FeedbackReportId { get; set; }

    [StringLength(20)]
    public string FeedbackType { get; set; } = null!;

    public Guid? InterviewFeedbackId { get; set; }

    public Guid? SystemFeedbackId { get; set; }

    public Guid ReporterUserId { get; set; }

    [StringLength(50)]
    public string ReasonCode { get; set; } = null!;

    [StringLength(1000)]
    public string? Details { get; set; }

    [StringLength(40)]
    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedBy { get; set; }

    [StringLength(1000)]
    public string? ResolutionNote { get; set; }

    [ForeignKey(nameof(InterviewFeedbackId))]
    public virtual InterviewFeedback? InterviewFeedback { get; set; }

    [ForeignKey(nameof(SystemFeedbackId))]
    public virtual SystemFeedback? SystemFeedback { get; set; }
}

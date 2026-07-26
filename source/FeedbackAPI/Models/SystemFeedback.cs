using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Models;

[Index("Category", Name = "IX_SysFb_Category")]
[Index("Status", "IsDeleted", Name = "IX_SysFb_Status")]
[Index("UserId", Name = "IX_SysFb_User")]
public partial class SystemFeedback
{
    [Key]
    public Guid FeedbackId { get; set; }

    public Guid UserId { get; set; }

    [StringLength(30)]
    public string Category { get; set; } = null!;

    [StringLength(255)]
    public string? Subject { get; set; }

    public string Content { get; set; } = null!;

    public byte? Rating { get; set; }

    public byte? UiUxRating { get; set; }

    public byte? UsabilityRating { get; set; }

    public byte? FeatureQualityRating { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    [StringLength(20)]
    public string ModerationStatus { get; set; } = "Pending";

    public bool IsVisible { get; set; }

    public DateTime? ModeratedAt { get; set; }

    public Guid? ModeratedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Feedback")]
    public virtual ICollection<FeedbackReply> FeedbackReplies { get; set; } = new List<FeedbackReply>();

    [InverseProperty(nameof(FeedbackReport.SystemFeedback))]
    public virtual ICollection<FeedbackReport> FeedbackReports { get; set; } = new List<FeedbackReport>();
}
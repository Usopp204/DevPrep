using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Models;

[Index("SessionId", Name = "IX_IntFb_Session")]
public partial class InterviewFeedback
{
    [Key]
    public Guid InterviewFeedbackId { get; set; }

    public Guid SessionId { get; set; }

    public Guid UserId { get; set; }

    public byte Rating { get; set; }

    public byte? QuestionQualityRating { get; set; }

    public byte? AiAccuracyRating { get; set; }

    public byte? ScoringFairnessRating { get; set; }

    public byte? FeedbackUsefulnessRating { get; set; }

    public string? Content { get; set; }

    public bool? IsHelpful { get; set; }

    [StringLength(20)]
    public string ModerationStatus { get; set; } = "Pending";

    public bool IsVisible { get; set; }

    public DateTime? ModeratedAt { get; set; }

    public Guid? ModeratedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    [InverseProperty(nameof(FeedbackReport.InterviewFeedback))]
    public virtual ICollection<FeedbackReport> FeedbackReports { get; set; } = new List<FeedbackReport>();
}
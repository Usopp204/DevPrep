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

    public string? Content { get; set; }

    public bool? IsHelpful { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

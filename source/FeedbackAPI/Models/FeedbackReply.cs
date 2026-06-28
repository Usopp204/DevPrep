using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Models;

[Index("FeedbackId", Name = "IX_Reply_Feedback")]
public partial class FeedbackReply
{
    [Key]
    public Guid ReplyId { get; set; }

    public Guid FeedbackId { get; set; }

    public Guid RepliedBy { get; set; }

    public string ReplyContent { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    [ForeignKey("FeedbackId")]
    [InverseProperty("FeedbackReplies")]
    public virtual SystemFeedback Feedback { get; set; } = null!;
}

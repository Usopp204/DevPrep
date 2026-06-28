using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Models;

[Index("SessionQuestionId", "SentAt", Name = "IX_Msg_SessionQuestion")]
public partial class ChatMessage
{
    [Key]
    public Guid MessageId { get; set; }

    public Guid SessionQuestionId { get; set; }

    [StringLength(10)]
    public string SenderType { get; set; } = null!;

    [StringLength(20)]
    public string MessageType { get; set; } = null!;

    public string Content { get; set; } = null!;

    [StringLength(1024)]
    public string? AudioUrl { get; set; }

    public int? ResponseTimeMs { get; set; }

    public DateTime SentAt { get; set; }

    [ForeignKey("SessionQuestionId")]
    [InverseProperty("ChatMessages")]
    public virtual SessionQuestion SessionQuestion { get; set; } = null!;
}

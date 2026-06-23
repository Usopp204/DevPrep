using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Models;

[Index("SessionId", "OrderIndex", Name = "IX_SQ_Session")]
public partial class SessionQuestion
{
    [Key]
    public Guid SessionQuestionId { get; set; }

    public Guid SessionId { get; set; }

    public Guid? QuestionId { get; set; }

    public int OrderIndex { get; set; }

    public string Content { get; set; } = null!;

    public bool IsAiGenerated { get; set; }

    [StringLength(50)]
    public string? ContextSource { get; set; }

    public DateTime? AskedAt { get; set; }

    [InverseProperty("SessionQuestion")]
    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    [ForeignKey("SessionId")]
    [InverseProperty("SessionQuestions")]
    public virtual InterviewSession Session { get; set; } = null!;
}

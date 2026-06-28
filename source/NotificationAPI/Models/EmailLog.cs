using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NotificationAPI.Models;

[Index("Status", Name = "IX_Email_Status")]
public partial class EmailLog
{
    [Key]
    public Guid EmailLogId { get; set; }

    [StringLength(256)]
    public string RecipientEmail { get; set; } = null!;

    public Guid? UserId { get; set; }

    [StringLength(50)]
    public string? TemplateCode { get; set; }

    [StringLength(255)]
    public string Subject { get; set; } = null!;

    public string? Body { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

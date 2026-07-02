using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NotificationAPI.Models;

[Index("Code", Name = "UQ_EmailTemplates_Code", IsUnique = true)]
public partial class EmailTemplate
{
    [Key]
    public Guid TemplateId { get; set; }

    [StringLength(50)]
    public string Code { get; set; } = null!;

    [StringLength(255)]
    public string Subject { get; set; } = null!;

    public string BodyHtml { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}

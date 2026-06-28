using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AuthAPI.Models;

public partial class LoginAudit
{
    [Key]
    public Guid LoginAuditId { get; set; }

    public Guid? AccountId { get; set; }

    [StringLength(256)]
    public string? Email { get; set; }

    public bool IsSuccess { get; set; }

    [StringLength(20)]
    public string Method { get; set; } = null!;

    [StringLength(64)]
    public string? IpAddress { get; set; }

    [StringLength(512)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AuthAPI.Models;

[Index("AccountId", Name = "IX_BlockHist_Account")]
public partial class AccountBlockHistory
{
    [Key]
    public Guid BlockHistoryId { get; set; }

    public Guid AccountId { get; set; }

    [StringLength(10)]
    public string Action { get; set; } = null!;

    [StringLength(500)]
    public string? Reason { get; set; }

    public Guid PerformedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("AccountBlockHistories")]
    public virtual Account Account { get; set; } = null!;
}

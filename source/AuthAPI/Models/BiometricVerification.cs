using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AuthAPI.Models;

[Index("AccountId", Name = "IX_Bio_Account")]
public partial class BiometricVerification
{
    [Key]
    public Guid BiometricId { get; set; }

    public Guid AccountId { get; set; }

    [StringLength(10)]
    public string BiometricType { get; set; } = null!;

    [StringLength(1024)]
    public string? ReferenceUrl { get; set; }

    public string? EmbeddingJson { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Column(TypeName = "decimal(5, 4)")]
    public decimal? ConfidenceScore { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("BiometricVerifications")]
    public virtual Account Account { get; set; } = null!;
}

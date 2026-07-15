using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Models;

// Audit trail for every entry-gate and randomized in-session biometric check
// (Milestone 4 groundwork). One row per verification attempt.
[Table("SessionVerificationEvents")]
[Index("SessionId", "Phase", Name = "IX_SVE_Session_Phase")]
public partial class SessionVerificationEvent
{
    [Key]
    public long Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid UserId { get; set; }

    [StringLength(16)]
    public string Phase { get; set; } = null!;

    [StringLength(16)]
    public string Modality { get; set; } = null!;

    public double? Score { get; set; }

    public bool? LivenessPassed { get; set; }

    public bool Matched { get; set; }

    [StringLength(16)]
    public string Decision { get; set; } = null!;

    public string? Detail { get; set; }

    public DateTime CreatedAt { get; set; }
}

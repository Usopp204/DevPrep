using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Models;

// One server-issued verification challenge per request. The expected value
// (ChallengeJson) stays server-side; the client only receives the instruction.
// ConsumedAt is set on the first verify attempt so a challenge can't be replayed.
[Table("SessionVerificationChallenges")]
[Index("SessionId", "Modality", Name = "IX_SVC_Session_Modality")]
public partial class SessionVerificationChallenge
{
    [Key]
    public long Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid UserId { get; set; }

    [StringLength(16)]
    public string Modality { get; set; } = null!;

    [StringLength(400)]
    public string ChallengeJson { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

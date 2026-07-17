using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Models;

// One enrolled biometric template per (UserId, Modality). Stores the embedding
// vector only — never raw images/audio (privacy: Decree 13/2023).
[Table("ProfileBiometrics")]
[Index("UserId", "Modality", Name = "UX_ProfileBiometrics_User_Modality", IsUnique = true)]
public partial class ProfileBiometric
{
    [Key]
    public long Id { get; set; }

    public Guid UserId { get; set; }

    [StringLength(16)]
    public string Modality { get; set; } = null!;

    public byte[] Embedding { get; set; } = null!;

    public int Dim { get; set; }

    [StringLength(128)]
    public string ModelVersion { get; set; } = null!;

    public double? Quality { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

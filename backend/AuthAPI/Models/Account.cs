using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AuthAPI.Models;

[Index("Status", Name = "IX_Accounts_Status")]
[Index("Email", Name = "UQ_Accounts_Email", IsUnique = true)]
public partial class Account
{
    [Key]
    public Guid AccountId { get; set; }

    [StringLength(256)]
    public string Email { get; set; } = null!;

    [StringLength(512)]
    public string? PasswordHash { get; set; }

    [StringLength(20)]
    public string Role { get; set; } = null!;

    [StringLength(20)]
    public string Status { get; set; } = null!;

    public bool IsEmailVerified { get; set; }

    public bool IsFaceVerified { get; set; }

    public bool IsVoiceVerified { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTime? LockedUntil { get; set; }

    [StringLength(500)]
    public string? BlockedReason { get; set; }

    public DateTime? BlockedAt { get; set; }

    public Guid? BlockedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Account")]
    public virtual ICollection<AccountBlockHistory> AccountBlockHistories { get; set; } = new List<AccountBlockHistory>();

    [InverseProperty("Account")]
    public virtual ICollection<BiometricVerification> BiometricVerifications { get; set; } = new List<BiometricVerification>();

    [InverseProperty("Account")]
    public virtual ICollection<EmailVerificationToken> EmailVerificationTokens { get; set; } = new List<EmailVerificationToken>();

    [InverseProperty("Account")]
    public virtual ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();

    [InverseProperty("Account")]
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    [InverseProperty("Account")]
    public virtual ICollection<SsoIdentity> SsoIdentities { get; set; } = new List<SsoIdentity>();
}

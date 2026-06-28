using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AuthAPI.Models;

[Index("AccountId", Name = "IX_Refresh_Account")]
[Index("Token", Name = "IX_Refresh_Token")]
public partial class RefreshToken
{
    [Key]
    public Guid RefreshTokenId { get; set; }

    public Guid AccountId { get; set; }

    [StringLength(512)]
    public string Token { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime? RevokedAt { get; set; }

    [StringLength(64)]
    public string? IpAddress { get; set; }

    [StringLength(512)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("RefreshTokens")]
    public virtual Account Account { get; set; } = null!;
}

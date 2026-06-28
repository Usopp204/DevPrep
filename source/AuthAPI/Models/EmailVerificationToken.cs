using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AuthAPI.Models;

[Index("Token", Name = "IX_EmailVer_Token")]
public partial class EmailVerificationToken
{
    [Key]
    public Guid TokenId { get; set; }

    public Guid AccountId { get; set; }

    [StringLength(256)]
    public string Token { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("EmailVerificationTokens")]
    public virtual Account Account { get; set; } = null!;
}

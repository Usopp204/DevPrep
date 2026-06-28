using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AuthAPI.Models;

[Index("Provider", "ProviderUserId", Name = "UQ_Sso_Provider", IsUnique = true)]
public partial class SsoIdentity
{
    [Key]
    public Guid SsoIdentityId { get; set; }

    public Guid AccountId { get; set; }

    [StringLength(30)]
    public string Provider { get; set; } = null!;

    [StringLength(256)]
    public string ProviderUserId { get; set; } = null!;

    [StringLength(256)]
    public string? ProviderEmail { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("SsoIdentities")]
    public virtual Account Account { get; set; } = null!;
}

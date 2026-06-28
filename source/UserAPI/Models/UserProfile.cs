using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UserAPI.Models;

[Index("FullName", Name = "IX_User_FullName")]
[Index("AccountId", Name = "UQ_UserProfiles_Account", IsUnique = true)]
public partial class UserProfile
{
    [Key]
    public Guid UserId { get; set; }

    public Guid AccountId { get; set; }

    [StringLength(150)]
    public string? FullName { get; set; }

    [StringLength(1024)]
    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

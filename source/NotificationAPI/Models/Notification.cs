using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NotificationAPI.Models;

[Index("UserId", "IsRead", Name = "IX_Notif_User")]
public partial class Notification
{
    [Key]
    public Guid NotificationId { get; set; }

    public Guid UserId { get; set; }

    [StringLength(255)]
    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    [StringLength(30)]
    public string Type { get; set; } = null!;

    public bool IsRead { get; set; }

    [StringLength(1024)]
    public string? LinkUrl { get; set; }

    public DateTime CreatedAt { get; set; }
}

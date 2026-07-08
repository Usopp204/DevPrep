using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Models;

[Index("SessionId", Name = "UQ_Room_Session", IsUnique = true)]
public partial class InterviewRoom
{
    [Key]
    public Guid RoomId { get; set; }

    public Guid SessionId { get; set; }

    [StringLength(128)]
    public string ChannelName { get; set; } = null!;

    [StringLength(128)]
    public string? ConnectionId { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    public DateTime InitializedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    [ForeignKey("SessionId")]
    [InverseProperty("InterviewRoom")]
    public virtual InterviewSession Session { get; set; } = null!;
}

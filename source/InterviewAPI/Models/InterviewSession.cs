using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Models;

[Index("Status", Name = "IX_Sess_Status")]
[Index("UserId", "IsDeleted", Name = "IX_Sess_User")]
public partial class InterviewSession
{
    [Key]
    public Guid SessionId { get; set; }

    public Guid UserId { get; set; }

    public Guid? CvId { get; set; }

    public Guid? JdId { get; set; }

    [StringLength(100)]
    public string? Position { get; set; }

    public int DurationMinutes { get; set; }

    [StringLength(20)]
    public string Level { get; set; } = null!;

    [StringLength(20)]
    public string InteractionMode { get; set; } = null!;

    [StringLength(20)]
    public string QuestionType { get; set; } = null!;

    [StringLength(20)]
    public string Status { get; set; } = null!;

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    // Pause/Resume (Room UX Phase 2). TotalPausedSeconds accumulates every COMPLETED pause
    // interval; PausedAt marks the start of an ONGOING pause (null when not paused). Every timer
    // formula in SessionService must subtract the effective paused seconds — see
    // GetEffectivePausedSeconds.
    public int TotalPausedSeconds { get; set; }

    public DateTime? PausedAt { get; set; }

    public int PauseCount { get; set; }

    // Mandatory-camera policy audit counter (Room UX): permission denied or the camera
    // disconnecting mid-session. Strict by default (Biometrics:InSessionStrictness="Strict") —
    // ends the session once InSessionMaxStrikes is reached; set Strictness="Flag" to only
    // record occurrences for later review without blocking entry or answering.
    public int CameraViolationCount { get; set; }

    public int TotalQuestions { get; set; }

    public int AnsweredQuestions { get; set; }

    public int? AvgResponseTimeMs { get; set; }

    public int CreditCost { get; set; }

    public bool IntegrityPolicyAccepted { get; set; }

    public DateTime? IntegrityPolicyAcceptedAt { get; set; }

    // Last time "Skip" was used in this session — enforces Interview:SkipCooldownSeconds
    // (SessionRepository.TryConsumeSkipCooldownAsync). Despite the "Utc" suffix, this column
    // actually stores VietnamTime.Now like every other timestamp in this schema (AskedAt,
    // CreatedAt, IntegrityPolicyAcceptedAt, ...) — named per the task spec, not a new UTC
    // convention. DateDiffSecond only needs both sides on the same clock, which this preserves.
    public DateTime? LastSkipAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Session")]
    public virtual InterviewRoom? InterviewRoom { get; set; }

    [InverseProperty("Session")]
    public virtual ICollection<SessionQuestion> SessionQuestions { get; set; } = new List<SessionQuestion>();
}

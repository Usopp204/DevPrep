using System;
using System.ComponentModel.DataAnnotations;

namespace InterviewAPI.Models;

// Room UX Phase 3 (Product Tour). One row per user, ever — NOT per session, so the onboarding
// tour auto-shows exactly once in the user's lifetime regardless of how many interview sessions
// they start afterward. Deliberately has no FK to a Users table: InterviewAPI already treats
// UserId as an opaque Guid everywhere (e.g. InterviewSession.UserId), so this avoids touching
// UserAPI/AuthAPI for a flag that only this service's UI (the Room) cares about.
public partial class UserRoomTourStatus
{
    [Key]
    public Guid UserId { get; set; }

    public bool HasSeenRoomTour { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

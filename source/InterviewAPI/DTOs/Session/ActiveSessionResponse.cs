namespace InterviewAPI.DTOs.Session;

// The user's newest InProgress/Paused session, if any — surfaced on the Dashboard so an
// interview abandoned mid-session (tab closed, connection lost, ...) stays reachable. Recent
// Sessions/History intentionally exclude non-terminal statuses, so without this there was no
// way back to it except a bookmarked Room URL.
public class ActiveSessionResponse
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public int DurationMinutes { get; set; }
    public int RemainingSeconds { get; set; }
}

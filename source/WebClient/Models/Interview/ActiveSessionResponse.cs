namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.ActiveSessionResponse.
public class ActiveSessionResponse
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public int DurationMinutes { get; set; }
    public int RemainingSeconds { get; set; }
}

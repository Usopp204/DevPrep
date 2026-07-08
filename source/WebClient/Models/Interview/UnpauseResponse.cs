namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.UnpauseResponse.
public class UnpauseResponse
{
    public string Status { get; set; } = string.Empty;
    public int RemainingSeconds { get; set; }
    public int PauseCount { get; set; }
    public int PausesRemaining { get; set; }
    public int PausedSecondsRemaining { get; set; }
}

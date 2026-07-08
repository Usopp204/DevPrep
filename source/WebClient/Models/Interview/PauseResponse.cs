namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.PauseResponse.
public class PauseResponse
{
    public string Status { get; set; } = string.Empty;
    public int PauseCount { get; set; }
    public int PausesRemaining { get; set; }
    public int PausedSecondsRemaining { get; set; }
}

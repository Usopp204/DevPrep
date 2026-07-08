namespace InterviewAPI.DTOs.Session;

public class PauseResponse
{
    public string Status { get; set; } = string.Empty;
    public int PauseCount { get; set; }
    public int PausesRemaining { get; set; }
    public int PausedSecondsRemaining { get; set; }
}

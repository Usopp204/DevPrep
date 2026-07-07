namespace InterviewAPI.DTOs.Session;

public class ResumeResponse
{
    public string Status { get; set; } = string.Empty;
    // Session config, used by the Room UI to compose the static greeting line.
    public string? Position { get; set; }
    public string Level { get; set; } = string.Empty;
    public int RemainingSeconds { get; set; }
    public NextQuestionDto? CurrentQuestion { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredQuestions { get; set; }
    // Pause/Resume quota (Room UX Phase 2) — lets the Room render the overlay/quota immediately
    // on page load (e.g. a refresh mid-pause) without a separate round trip.
    public int PauseCount { get; set; }
    public int PausesRemaining { get; set; }
    public int PausedSecondsRemaining { get; set; }
    // Room UX Phase 3 — true once this user has ever dismissed/completed the onboarding tour.
    public bool HasSeenRoomTour { get; set; }
    public List<ChatHistoryItem> ChatHistory { get; set; } = new();
}

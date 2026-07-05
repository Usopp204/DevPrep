namespace WebClient.Models.Interview;

// Backs Views/Interview/Room.cshtml — initial state for the chat room, fetched via Resume.
// The page's own JS drives the countdown timer and chat exchange; chat calls go to same-origin
// WebClient proxy actions (Message/EndSession) so the HttpOnly JWT cookie is used server-side.
public class RoomViewModel
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    // Session config used to compose the static greeting bubble (display only, never stored).
    public string? Position { get; set; }
    public string Level { get; set; } = string.Empty;
    public int RemainingSeconds { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredQuestions { get; set; }
    public Guid? CurrentQuestionId { get; set; }
    public List<ChatHistoryItem> ChatHistory { get; set; } = new();
}

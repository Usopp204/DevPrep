namespace InterviewAPI.DTOs.Session;

public class SendMessageRequest
{
    public Guid SessionQuestionId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? ResponseTimeMs { get; set; }

    // Client hint only — null, "Skipped", or "TimedOut". The server re-verifies "TimedOut" against
    // SessionQuestion.AskedAt before trusting it (see SessionService.SendMessageAsync).
    public string? Reason { get; set; }
}

namespace WebClient.Models.Interview;

// Identifies which question the Room asks a hint for (same-origin proxy to the Gateway).
// No question text here — the server looks it up itself from SessionQuestionId.
public class HintProxyRequest
{
    public Guid SessionId { get; set; }
    public Guid SessionQuestionId { get; set; }
}

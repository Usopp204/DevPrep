namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.ConfigInteractionRequest.
public class ConfigInteractionRequest
{
    public Guid? SessionId { get; set; }
    public string Mode { get; set; } = string.Empty;
}

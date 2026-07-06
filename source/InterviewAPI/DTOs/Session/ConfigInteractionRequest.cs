namespace InterviewAPI.DTOs.Session;

public class ConfigInteractionRequest
{
    public Guid? SessionId { get; set; }
    public string Mode { get; set; } = string.Empty;
}

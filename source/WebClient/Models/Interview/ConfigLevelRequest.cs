namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.ConfigLevelRequest.
public class ConfigLevelRequest
{
    public Guid? SessionId { get; set; }
    public string Level { get; set; } = string.Empty;
}

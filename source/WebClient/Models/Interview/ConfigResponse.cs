namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.ConfigResponse.
public class ConfigResponse
{
    public Guid SessionId { get; set; }
    public string UpdatedField { get; set; } = string.Empty;
    public string UpdatedValue { get; set; } = string.Empty;
}

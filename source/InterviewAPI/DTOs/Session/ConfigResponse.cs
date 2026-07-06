namespace InterviewAPI.DTOs.Session;

public class ConfigResponse
{
    public Guid SessionId { get; set; }
    public string UpdatedField { get; set; } = string.Empty;
    public string UpdatedValue { get; set; } = string.Empty;
}

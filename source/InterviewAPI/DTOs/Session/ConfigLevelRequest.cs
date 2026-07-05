namespace InterviewAPI.DTOs.Session;

public class ConfigLevelRequest
{
    public Guid? SessionId { get; set; }
    public string Level { get; set; } = string.Empty;
}

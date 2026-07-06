namespace InterviewAPI.DTOs.Session;

public class ConfigDurationRequest
{
    public Guid? SessionId { get; set; }
    public int DurationMinutes { get; set; }
}

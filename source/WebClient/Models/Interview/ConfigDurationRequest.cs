namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.ConfigDurationRequest.
public class ConfigDurationRequest
{
    public Guid? SessionId { get; set; }
    public int DurationMinutes { get; set; }
}

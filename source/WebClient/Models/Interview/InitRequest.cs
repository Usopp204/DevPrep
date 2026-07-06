namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.InitRequest.
public class InitRequest
{
    public Guid SessionId { get; set; }
    public string? Position { get; set; }
    public string? Jd { get; set; }
    public List<string>? FocusAreas { get; set; }
}

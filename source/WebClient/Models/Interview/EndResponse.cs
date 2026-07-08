namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.EndResponse.
public class EndResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime? EndedAt { get; set; }
}

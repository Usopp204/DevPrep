namespace InterviewAPI.DTOs.Session;

public class InitRequest
{
    public Guid SessionId { get; set; }

    // Role/JD/Focus chosen on the dashboard — fed to ai-service so the RAG plan is tailored.
    public string? Position { get; set; }
    public string? Jd { get; set; }
    public List<string>? FocusAreas { get; set; }
}

namespace InterviewAPI.DTOs.Session;

public class InitRequest
{
    public Guid SessionId { get; set; }

    // Role/JD/Focus chosen on the dashboard — fed to ai-service so the RAG plan is tailored.
    public string? Position { get; set; }
    public string? Jd { get; set; }

    // The CvJdAPI JobDescription this session's JD text came from (shared JD between
    // Profile/CvJd and the Interview dashboard). Purely informational on this side —
    // ai-service still gets the resolved raw text via Jd above.
    public Guid? JdId { get; set; }
    public List<string>? FocusAreas { get; set; }
}

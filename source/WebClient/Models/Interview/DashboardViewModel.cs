namespace WebClient.Models.Interview;

// Backs Views/Interview/Index.cshtml - the interview configuration + start form.
public class DashboardViewModel
{
    public int DurationMinutes { get; set; } = 30;
    public string Level { get; set; } = "Junior";

    // Role, JD and focus technologies now drive the ai-service question plan (loaded from /catalog).
    public string Position { get; set; } = "Backend";
    public string? Jd { get; set; }
    public List<string> FocusAreas { get; set; } = new();

    // Newest completed/expired sessions shown in the sidebar "Recent Sessions" list.
    public List<SessionHistoryItemDto> RecentSessions { get; set; } = new();
    public InterviewSidebarViewModel Sidebar { get; set; } = new() { ActiveItem = "New" };

    public string? ErrorMessage { get; set; }
}
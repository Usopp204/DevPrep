namespace WebClient.Models.Interview;

public class InterviewSidebarViewModel
{
    public string ActiveItem { get; set; } = "New";
    public Guid? ActiveSessionId { get; set; }
    public List<SessionHistoryItemDto> RecentSessions { get; set; } = new();
    public string? RecentError { get; set; }
}
namespace WebClient.Models.Home;

/// <summary>Một thông báo hệ thống (NotificationAPI GET /announcements) hiển thị popup homepage.</summary>
public class AnnouncementItem
{
    public Guid AnnouncementId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

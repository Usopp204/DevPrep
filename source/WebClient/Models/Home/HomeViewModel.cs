using WebClient.Models.Feedback;

namespace WebClient.Models.Home;

public class HomeViewModel
{
    public bool ShowSystemFeedbackPopup { get; set; }
    public SubmitSystemFeedbackRequest SystemFeedbackForm { get; set; } = new();
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FeedbackValidationMessage { get; set; }
    public bool FeedbackFormWasSubmitted { get; set; }

    /// <summary>Thông báo hệ thống đang bật — hiện popup cho cả guest lẫn user khi vào trang.</summary>
    public List<AnnouncementItem> Announcements { get; set; } = new();
}
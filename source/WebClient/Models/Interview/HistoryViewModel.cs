namespace WebClient.Models.Interview;

public class HistoryViewModel
{
    public SessionHistoryResult Sessions { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 5;
    public string? Status { get; set; }
    public string? Position { get; set; }
    public string? QuestionType { get; set; }
    public string? Keyword { get; set; }
    public string? ErrorMessage { get; set; }
    public int LoadStatusCode { get; set; }
    public string? SuccessMessage { get; set; }
    public InterviewSidebarViewModel Sidebar { get; set; } = new() { ActiveItem = "History" };
}
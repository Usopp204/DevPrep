using WebClient.Models.Shared;

namespace WebClient.Models.Interview;

public class SessionHistoryResult : PagedResult<SessionHistoryItemDto>
{
    public List<string> PositionOptions { get; set; } = new();
    public List<string> QuestionTypeOptions { get; set; } = new();
}

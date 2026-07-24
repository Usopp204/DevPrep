using InterviewAPI.DTOs;

namespace InterviewAPI.DTOs.Session;

public class SessionHistoryResult : PagedResult<SessionHistoryItemDto>
{
    public IReadOnlyList<string> PositionOptions { get; set; } = Array.Empty<string>();
}

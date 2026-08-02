namespace InterviewAPI.DTOs;

public sealed class InterviewDashboardStatisticsDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public long TotalInterviewSessions { get; set; }
    public long CompletedInterviewSessions { get; set; }
    public List<InterviewDashboardStatisticsDayDto> Days { get; set; } = new();
}

public sealed class InterviewDashboardStatisticsDayDto
{
    public DateOnly Date { get; set; }
    public long Count { get; set; }
}

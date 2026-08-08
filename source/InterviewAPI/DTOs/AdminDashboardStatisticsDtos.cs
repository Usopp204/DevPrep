namespace InterviewAPI.DTOs;

public sealed class InterviewDashboardStatisticsDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public long TotalInterviewSessionsAsOf { get; set; }
    public long CompletedInterviewSessionsAsOf { get; set; }
    public List<InterviewDashboardStatisticsDayDto> Days { get; set; } = new();
}

public sealed class InterviewDashboardStatisticsDayDto
{
    public DateOnly Date { get; set; }
    public long CreatedCount { get; set; }
    public long CompletedCount { get; set; }
}

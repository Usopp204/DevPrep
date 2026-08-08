namespace FeedbackAPI.DTOs;

public sealed class FeedbackDashboardStatisticsDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public long TotalSystemFeedbacksAsOf { get; set; }
    public long TotalInterviewFeedbacksAsOf { get; set; }
    public long TotalFeedbacksAsOf { get; set; }
    public List<FeedbackDashboardStatisticsDayDto> Days { get; set; } = new();
}

public sealed class FeedbackDashboardStatisticsDayDto
{
    public DateOnly Date { get; set; }
    public long SystemFeedbacks { get; set; }
    public long InterviewFeedbacks { get; set; }
    public long TotalFeedbacks { get; set; }
}
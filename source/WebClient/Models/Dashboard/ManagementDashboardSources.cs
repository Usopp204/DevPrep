namespace WebClient.Models.Dashboard;

public sealed class ManagementCountDay
{
    public required DateOnly Date { get; set; }
    public required long Count { get; set; }
}

public sealed class CandidateStatisticsSourceData
{
    public required DateOnly From { get; set; }
    public required DateOnly To { get; set; }
    public required long TotalCandidatesAsOf { get; set; }
    public required List<ManagementCountDay> Days { get; set; } = new();
}

public sealed class ManagementInterviewDay
{
    public required DateOnly Date { get; set; }
    public required long CreatedCount { get; set; }
    public required long CompletedCount { get; set; }
}

public sealed class InterviewDashboardSourceData
{
    public required DateOnly From { get; set; }
    public required DateOnly To { get; set; }
    public required long TotalInterviewSessionsAsOf { get; set; }
    public required long CompletedInterviewSessionsAsOf { get; set; }
    public required List<ManagementInterviewDay> Days { get; set; } = new();
}

public sealed class ManagementRevenueDay
{
    public required DateOnly Date { get; set; }
    public required decimal Revenue { get; set; }
    public required long Orders { get; set; }
}

public sealed class RevenueDailySourceData
{
    public required DateOnly From { get; set; }
    public required DateOnly To { get; set; }
    public required decimal TotalRevenueAsOf { get; set; }
    public required long TotalOrdersAsOf { get; set; }
    public required decimal PeriodRevenue { get; set; }
    public required long PeriodOrders { get; set; }
    public required List<ManagementRevenueDay> Days { get; set; } = new();
}

public sealed class ManagementFeedbackDay
{
    public required DateOnly Date { get; set; }
    public required long SystemFeedbacks { get; set; }
    public required long InterviewFeedbacks { get; set; }
    public required long TotalFeedbacks { get; set; }
}

public sealed class FeedbackDashboardSourceData
{
    public required DateOnly From { get; set; }
    public required DateOnly To { get; set; }
    public required long TotalSystemFeedbacksAsOf { get; set; }
    public required long TotalInterviewFeedbacksAsOf { get; set; }
    public required long TotalFeedbacksAsOf { get; set; }
    public required List<ManagementFeedbackDay> Days { get; set; } = new();
}

namespace WebClient.Models.Dashboard;

public sealed class DashboardCountDay
{
    public DateOnly Date { get; set; }
    public long Count { get; set; }
}

public sealed class CandidateDashboardSource
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public long TotalCandidates { get; set; }
    public List<DashboardCountDay> Days { get; set; } = new();
}

public sealed class InterviewDashboardSource
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public long TotalInterviewSessions { get; set; }
    public long CompletedInterviewSessions { get; set; }
    public List<DashboardCountDay> Days { get; set; } = new();
}

public sealed class DashboardRevenueDay
{
    public DateOnly Date { get; set; }
    public decimal Revenue { get; set; }
    public long Orders { get; set; }
}

public sealed class RevenueDashboardSource
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public decimal TotalRevenue { get; set; }
    public long TotalOrders { get; set; }
    public List<DashboardRevenueDay> Days { get; set; } = new();
}

public sealed class DashboardFeedbackDay
{
    public DateOnly Date { get; set; }
    public long SystemFeedbacks { get; set; }
    public long InterviewFeedbacks { get; set; }
    public long TotalFeedbacks { get; set; }
}

public sealed class FeedbackDashboardSource
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public long AllTimeSystemFeedbacks { get; set; }
    public long AllTimeInterviewFeedbacks { get; set; }
    public long AllTimeTotalFeedbacks { get; set; }
    public List<DashboardFeedbackDay> Days { get; set; } = new();
}

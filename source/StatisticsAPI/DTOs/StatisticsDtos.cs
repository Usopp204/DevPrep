namespace StatisticsAPI.DTOs;

public class StatisticsOverviewDto
{
    public long TotalUsers { get; set; }
    public long ActiveUsers { get; set; }
    public long NewUsersThisMonth { get; set; }
    public long TotalSessions { get; set; }
    public long CompletedSessions { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public long PaidOrders { get; set; }
    public long TotalFeedbacks { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TimeSeriesResult
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalPaidOrders { get; set; }
    public int TotalNewUsers { get; set; }
    public List<TimeSeriesPoint> Days { get; set; } = new();
}

public class TimeSeriesPoint
{
    public DateOnly Date { get; set; }
    public int NewUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InterviewSessions { get; set; }
    public int CompletedSessions { get; set; }
    public decimal Revenue { get; set; }
    public int PaidOrders { get; set; }
    public int NewFeedbacks { get; set; }
}

public sealed class ManagementDashboardDto
{
    public bool Available { get; set; }
    public bool Stale { get; set; }
    public DateTime? LastUpdated { get; set; }
    public long? TotalCandidates { get; set; }
    public long? NewCandidatesThisMonth { get; set; }
    public long? TotalInterviewSessions { get; set; }
    public long? InterviewSessionsThisMonth { get; set; }
    public long? CompletedInterviewSessions { get; set; }
    public decimal? GrossVnpayRevenueThisMonth { get; set; }
    public long? TotalFeedbacks { get; set; }
    public long? FeedbacksThisMonth { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string Granularity { get; set; } = "Daily";
    public decimal? SelectedRangeGrossVnpayRevenue { get; set; }
    public long? SelectedRangePaidVnpayOrders { get; set; }
    public List<RevenueBucketDto> RevenueBuckets { get; set; } = new();
}

public sealed class RevenueBucketDto
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public long Orders { get; set; }
}

public class RebuildResultDto
{
    public bool Ok { get; set; }
    public bool Busy { get; set; }
    public int SnapshotsWritten { get; set; }
    public int CountersWritten { get; set; }
    public DateTime FinishedAt { get; set; }
    public StatisticsOverviewDto? Overview { get; set; }
    public List<string> Warnings { get; set; } = new();
}
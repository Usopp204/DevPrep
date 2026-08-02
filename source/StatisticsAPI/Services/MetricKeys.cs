namespace StatisticsAPI.Services;

/// <summary>Danh sách key chuẩn của MetricCounters — tránh gõ tay sai giữa collector và query service.</summary>
public static class MetricKeys
{
    public const string TotalUsers = "TotalUsers";
    public const string ActiveUsers = "ActiveUsers";
    public const string NewUsersThisMonth = "NewUsersThisMonth";
    public const string TotalSessions = "TotalSessions";
    public const string CompletedSessions = "CompletedSessions";
    public const string TotalRevenue = "TotalRevenue";
    public const string RevenueThisMonth = "RevenueThisMonth";
    public const string PaidOrders = "PaidOrders";
    public const string TotalFeedbacks = "TotalFeedbacks";
    public const string DashboardTotalCandidates = "DashboardTotalCandidates";
    public const string DashboardTotalInterviewSessions = "DashboardTotalInterviewSessions";
    public const string DashboardCompletedInterviewSessions = "DashboardCompletedInterviewSessions";
    public const string DashboardTotalFeedbacks = "DashboardTotalFeedbacks";
    public const string DashboardGeneration = "DashboardGeneration";
    public const string DashboardWindowFrom = "DashboardWindowFrom";
    public const string DashboardWindowTo = "DashboardWindowTo";
}

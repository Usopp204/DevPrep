namespace StatisticsAPI.DTOs;

/// <summary>Các con số tổng "hiện tại" đọc từ MetricCounters — đủ cho các stat card của Admin Dashboard.</summary>
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

    /// <summary>Lần cập nhật gần nhất của bất kỳ counter nào (null nếu bảng rỗng — chưa chạy collector).</summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Chuỗi số liệu theo ngày (từ StatisticsSnapshots) — vẽ biểu đồ xu hướng.</summary>
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

/// <summary>Kết quả sau khi chạy rebuild (tổng hợp lại từ các service).</summary>
public class RebuildResultDto
{
    public bool Ok { get; set; }
    public int SnapshotsWritten { get; set; }
    public int CountersWritten { get; set; }
    public DateTime FinishedAt { get; set; }
    public StatisticsOverviewDto? Overview { get; set; }
    public List<string> Warnings { get; set; } = new();
}

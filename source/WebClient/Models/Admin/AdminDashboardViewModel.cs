namespace WebClient.Models.Admin;

/// <summary>
/// Số liệu thật cho trang Admin Dashboard. Field nào null nghĩa là nguồn dữ liệu
/// tương ứng đang lỗi/không chạy — view hiển thị "—" thay vì số.
/// </summary>
public class AdminDashboardViewModel
{
    public long? TotalUsers { get; set; }
    public long? NewUsersThisMonth { get; set; }
    public long? TotalSessions { get; set; }
    public decimal? RevenueThisMonth { get; set; }

    /// <summary>Khoảng ngày mặc định cho chart (đầu tháng → hôm nay, giờ VN).</summary>
    public DateTime DefaultFrom { get; set; }
    public DateTime DefaultTo { get; set; }
}

/// <summary>Kết quả doanh thu theo từng ngày (khớp PaymentAPI /admin/payments/revenue-daily).</summary>
public sealed class RevenueDailyResult
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int TotalCredits { get; set; }
    public List<RevenueDailyPoint> Days { get; set; } = new();
}

public sealed class RevenueDailyPoint
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
    public int Credits { get; set; }
}

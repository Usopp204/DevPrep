namespace WebClient.Models.Manager;

public class ManagerDashboardViewModel
{
    public long? TotalUsers { get; set; }
    public long? NewUsersThisMonth { get; set; }
    public long? TotalSessions { get; set; }
    public decimal? RevenueThisMonth { get; set; }

    public DateTime DefaultFrom { get; set; }
    public DateTime DefaultTo { get; set; }
}

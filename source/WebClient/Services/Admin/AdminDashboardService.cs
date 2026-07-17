using System.Net.Http.Json;
using System.Text.Json;
using WebClient.Models.Admin;
using WebClient.Services.Shared;

namespace WebClient.Services.Admin;

public interface IAdminDashboardService
{
    /// <summary>Gom 4 số liệu thật cho các stat card (best-effort từng nguồn: nguồn nào lỗi thì null).</summary>
    Task<AdminDashboardViewModel> GetStatsAsync(CancellationToken ct = default);

    /// <summary>Doanh thu theo từng ngày trong [from..to] (PaymentAPI, qua gateway). Null nếu backend lỗi.</summary>
    Task<RevenueDailyResult?> GetRevenueDailyAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

/// <summary>
/// Lấy số liệu dashboard qua API Gateway (Bearer từ cookie do JwtDelegatingHandler tự gắn):
///   - 4 stat card (tổng user / user mới trong tháng / tổng phiên / doanh thu tháng):
///       StatisticsAPI GET api/admin/statistics/overview — MỘT nguồn read-model, KHÔNG fan-out 3-4 service.
///   - Chart doanh thu theo ngày: PaymentAPI GET api/payment/admin/payments/revenue-daily
///       (giữ nguồn Payment vì cần lịch sử đầy đủ + số credits mà snapshot 30 ngày chưa có).
/// Bọc try/catch — nguồn chết thì hiện "—" / chart báo lỗi, không kéo sập dashboard.
/// </summary>
public sealed class AdminDashboardService(IApiClient apiClient) : IAdminDashboardService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AdminDashboardViewModel> GetStatsAsync(CancellationToken ct = default)
    {
        var nowVn = DateTime.UtcNow.AddHours(7);
        var monthStart = new DateTime(nowVn.Year, nowVn.Month, 1);

        // Đọc TẤT CẢ số liệu stat card từ MỘT nguồn duy nhất: StatisticsAPI (read-model DevPrep_Statistics).
        // KHÔNG còn fan-out sang UserAPI/InterviewAPI/PaymentAPI mỗi lần load dashboard (job rollup của
        // StatisticsAPI mới là chỗ tổng hợp định kỳ từ các service). Statistics chết -> các số hiện "—".
        long? totalUsers = null, newUsers = null, totalSessions = null;
        decimal? revenue = null;

        var root = await GetJsonAsync("api/admin/statistics/overview", ct);
        if (root is not null)
        {
            try
            {
                var data = root.Value.GetProperty("data");
                totalUsers = data.GetProperty("totalUsers").GetInt64();
                newUsers = data.GetProperty("newUsersThisMonth").GetInt64();
                totalSessions = data.GetProperty("totalSessions").GetInt64();
                revenue = data.GetProperty("revenueThisMonth").GetDecimal();
            }
            catch { /* shape lạ -> để null (hiện "—") */ }
        }

        return new AdminDashboardViewModel
        {
            TotalUsers = totalUsers,
            NewUsersThisMonth = newUsers,
            TotalSessions = totalSessions,
            RevenueThisMonth = revenue,
            DefaultFrom = monthStart,
            DefaultTo = nowVn.Date
        };
    }

    public async Task<RevenueDailyResult?> GetRevenueDailyAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        try
        {
            return await apiClient.HttpClient.GetFromJsonAsync<RevenueDailyResult>(
                $"api/payment/admin/payments/revenue-daily?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", Json, ct);
        }
        catch
        {
            return null; // PaymentAPI/gateway chưa chạy -> chart hiện thông báo lỗi thay vì 500.
        }
    }

    private async Task<JsonElement?> GetJsonAsync(string url, CancellationToken ct)
    {
        try
        {
            using var resp = await apiClient.HttpClient.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }
}

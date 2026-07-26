using System.Net.Http.Json;
using System.Text.Json;
using WebClient.Models.Admin;
using WebClient.Models.Manager;
using WebClient.Services.Shared;

namespace WebClient.Services.Manager;

public interface IManagerDashboardService
{
    Task<ManagerDashboardViewModel> GetStatsAsync(CancellationToken ct = default);
    Task<RevenueDailyResult?> GetRevenueDailyAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

public sealed class ManagerDashboardService(IApiClient apiClient) : IManagerDashboardService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ManagerDashboardViewModel> GetStatsAsync(CancellationToken ct = default)
    {
        var nowVn = DateTime.UtcNow.AddHours(7);
        var monthStart = new DateTime(nowVn.Year, nowVn.Month, 1);

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
            catch { }
        }

        return new ManagerDashboardViewModel
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
            return null;
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

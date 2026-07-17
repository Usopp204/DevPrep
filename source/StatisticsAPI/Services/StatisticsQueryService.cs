using Microsoft.EntityFrameworkCore;
using StatisticsAPI.Data;
using StatisticsAPI.DTOs;

namespace StatisticsAPI.Services;

public interface IStatisticsQueryService
{
    Task<StatisticsOverviewDto> GetOverviewAsync(CancellationToken ct = default);
    Task<TimeSeriesResult> GetTimeSeriesAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}

/// <summary>
/// ĐỌC số liệu từ DevPrep_Statistics (KHÔNG gọi service khác) — đây là read-model, dashboard chỉ chạm 1 chỗ này.
/// </summary>
public sealed class StatisticsQueryService(StatisticsDbContext db) : IStatisticsQueryService
{
    public async Task<StatisticsOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var counters = await db.MetricCounters.AsNoTracking().ToListAsync(ct);
        var map = counters.ToDictionary(c => c.MetricKey, c => c.MetricValue, StringComparer.OrdinalIgnoreCase);

        long L(string key) => map.TryGetValue(key, out var v) ? (long)v : 0L;
        decimal D(string key) => map.TryGetValue(key, out var v) ? v : 0m;

        return new StatisticsOverviewDto
        {
            TotalUsers = L(MetricKeys.TotalUsers),
            ActiveUsers = L(MetricKeys.ActiveUsers),
            NewUsersThisMonth = L(MetricKeys.NewUsersThisMonth),
            TotalSessions = L(MetricKeys.TotalSessions),
            CompletedSessions = L(MetricKeys.CompletedSessions),
            TotalRevenue = D(MetricKeys.TotalRevenue),
            RevenueThisMonth = D(MetricKeys.RevenueThisMonth),
            PaidOrders = L(MetricKeys.PaidOrders),
            TotalFeedbacks = L(MetricKeys.TotalFeedbacks),
            UpdatedAt = counters.Count > 0 ? counters.Max(c => c.UpdatedAt) : null
        };
    }

    public async Task<TimeSeriesResult> GetTimeSeriesAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from) (from, to) = (to, from);

        var rows = await db.StatisticsSnapshots.AsNoTracking()
            .Where(s => s.SnapshotDate >= from && s.SnapshotDate <= to)
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync(ct);

        var days = rows.Select(s => new TimeSeriesPoint
        {
            Date = s.SnapshotDate,
            NewUsers = s.NewUsers,
            ActiveUsers = s.ActiveUsers,
            InterviewSessions = s.InterviewSessions,
            CompletedSessions = s.CompletedSessions,
            Revenue = s.Revenue,
            PaidOrders = s.PaidOrders,
            NewFeedbacks = s.NewFeedbacks
        }).ToList();

        return new TimeSeriesResult
        {
            From = from,
            To = to,
            TotalRevenue = days.Sum(d => d.Revenue),
            TotalPaidOrders = days.Sum(d => d.PaidOrders),
            TotalNewUsers = days.Sum(d => d.NewUsers),
            Days = days
        };
    }
}

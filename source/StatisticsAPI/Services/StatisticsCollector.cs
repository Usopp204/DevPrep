using Microsoft.EntityFrameworkCore;
using StatisticsAPI.Clients;
using StatisticsAPI.Data;
using StatisticsAPI.DTOs;
using StatisticsAPI.Helpers;
using StatisticsAPI.Models;

namespace StatisticsAPI.Services;

public interface IStatisticsCollector
{
    /// <summary>Tổng hợp lại số liệu từ các service → cập nhật MetricCounters + StatisticsSnapshots.</summary>
    Task<RebuildResultDto> RunAsync(CancellationToken ct = default);
}

/// <summary>
/// "Rollup job": kéo số liệu từ User/Interview/Feedback/Payment (qua HTTP) rồi ghi vào read-model DevPrep_Statistics.
/// Best-effort: nhóm nào lỗi thì bỏ qua + ghi warning, không kéo sập cả lần chạy.
/// Backfill snapshot theo ngày (incremental): chỉ tính ngày còn THIẾU + LUÔN tính lại ngày hôm nay.
/// </summary>
public sealed class StatisticsCollector(
    StatisticsDbContext db,
    ISourceMetricsClient source,
    IStatisticsQueryService queryService,
    IConfiguration configuration,
    ILogger<StatisticsCollector> logger) : IStatisticsCollector
{
    public async Task<RebuildResultDto> RunAsync(CancellationToken ct = default)
    {
        var warnings = new List<string>();
        var now = VietnamTime.Now;
        var today = VietnamTime.Today;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var backfillDays = Math.Clamp(configuration.GetValue("Statistics:BackfillDays", 30), 1, 365);

        // Lấy doanh thu theo ngày "all-time" 1 lần: vừa ra tổng (counter) vừa map theo ngày (snapshot).
        RevenueDailyData revenue = new(0m, 0, new());
        try
        {
            revenue = await source.GetRevenueDailyAsync(new DateOnly(2000, 1, 1), DateOnly.FromDateTime(today), ct);
        }
        catch (Exception ex) { warnings.Add("Payment revenue-daily: " + ex.Message); logger.LogWarning(ex, "revenue-daily failed"); }

        // ---------- 1) MetricCounters (con số tổng) ----------
        var counters = await db.MetricCounters.ToDictionaryAsync(c => c.MetricKey, ct);
        var countersWritten = 0;

        void SetCounter(string key, decimal value)
        {
            if (counters.TryGetValue(key, out var c)) { c.MetricValue = value; c.UpdatedAt = now; }
            else { var n = new MetricCounter { MetricKey = key, MetricValue = value, UpdatedAt = now }; db.MetricCounters.Add(n); counters[key] = n; }
            countersWritten++;
        }

        await Safe(warnings, "TotalUsers", async () => SetCounter(MetricKeys.TotalUsers, await source.GetUserCountAsync(null, null, null, ct)));
        await Safe(warnings, "ActiveUsers", async () => SetCounter(MetricKeys.ActiveUsers, await source.GetUserCountAsync(null, null, "Active", ct)));
        await Safe(warnings, "NewUsersThisMonth", async () => SetCounter(MetricKeys.NewUsersThisMonth, await source.GetUserCountAsync(monthStart, now, null, ct)));
        await Safe(warnings, "TotalSessions", async () => SetCounter(MetricKeys.TotalSessions, await source.GetInterviewCountAsync(null, null, null, includeDeleted: true, ct)));
        await Safe(warnings, "CompletedSessions", async () => SetCounter(MetricKeys.CompletedSessions, await source.GetInterviewCountAsync(null, null, "Completed", includeDeleted: true, ct)));
        await Safe(warnings, "RevenueThisMonth", async () => SetCounter(MetricKeys.RevenueThisMonth, await source.GetRevenueReportTotalAsync("month", ct)));
        await Safe(warnings, "TotalFeedbacks", async () =>
        {
            var ai = await source.GetFeedbackCountAsync("ai", null, null, ct);
            var sys = await source.GetFeedbackCountAsync("system", null, null, ct);
            SetCounter(MetricKeys.TotalFeedbacks, ai + sys);
        });
        // Tổng doanh thu + số đơn all-time lấy thẳng từ revenue-daily đã gọi ở trên.
        SetCounter(MetricKeys.TotalRevenue, revenue.TotalRevenue);
        SetCounter(MetricKeys.PaidOrders, revenue.TotalOrders);

        // ---------- 2) StatisticsSnapshots (theo ngày) ----------
        var windowStart = DateOnly.FromDateTime(today).AddDays(-(backfillDays - 1));
        var todayD = DateOnly.FromDateTime(today);

        var existing = await db.StatisticsSnapshots
            .Where(s => s.SnapshotDate >= windowStart && s.SnapshotDate <= todayD)
            .ToDictionaryAsync(s => s.SnapshotDate, ct);

        var snapshotsWritten = 0;
        for (var d = windowStart; d <= todayD; d = d.AddDays(1))
        {
            var isToday = d == todayD;
            var hasRow = existing.TryGetValue(d, out var row);
            // Incremental: ngày cũ đã có -> bỏ qua; chỉ tính ngày thiếu + luôn cập nhật hôm nay.
            if (hasRow && !isToday) continue;

            try
            {
                var dayStart = d.ToDateTime(TimeOnly.MinValue);          // 00:00:00
                var dayEnd = d.ToDateTime(new TimeOnly(23, 59, 59));     // 23:59:59

                var newUsers = await source.GetUserCountAsync(dayStart, dayEnd, null, ct);
                var sessions = await source.GetInterviewCountAsync(dayStart, dayEnd, null, includeDeleted: true, ct);
                var completed = await source.GetInterviewCountAsync(dayStart, dayEnd, "Completed", includeDeleted: true, ct);
                var fbAi = await source.GetFeedbackCountAsync("ai", dayStart, dayEnd, ct);
                var fbSys = await source.GetFeedbackCountAsync("system", dayStart, dayEnd, ct);
                var (rev, ord) = revenue.ByDay.TryGetValue(d, out var rv) ? rv : (0m, 0);

                if (!hasRow)
                {
                    row = new StatisticsSnapshot { SnapshotId = Guid.NewGuid(), SnapshotDate = d, CreatedAt = now };
                    db.StatisticsSnapshots.Add(row);
                }
                row!.NewUsers = (int)newUsers;
                row.ActiveUsers = 0; // chưa có nguồn tái dựng "active theo ngày" lịch sử -> để 0 (nâng cấp sau).
                row.InterviewSessions = (int)sessions;
                row.CompletedSessions = (int)completed;
                row.NewFeedbacks = (int)(fbAi + fbSys);
                row.Revenue = rev;
                row.PaidOrders = ord;
                snapshotsWritten++;
            }
            catch (Exception ex)
            {
                warnings.Add($"Snapshot {d:yyyy-MM-dd}: {ex.Message}");
                logger.LogWarning(ex, "snapshot {Date} failed", d);
            }
        }

        await db.SaveChangesAsync(ct);

        var overview = await queryService.GetOverviewAsync(ct);
        logger.LogInformation("Statistics rollup done: {Counters} counters, {Snapshots} snapshots, {Warnings} warnings",
            countersWritten, snapshotsWritten, warnings.Count);

        return new RebuildResultDto
        {
            Ok = warnings.Count == 0,
            CountersWritten = countersWritten,
            SnapshotsWritten = snapshotsWritten,
            FinishedAt = now,
            Overview = overview,
            Warnings = warnings
        };
    }

    private static async Task Safe(List<string> warnings, string label, Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { warnings.Add($"{label}: {ex.Message}"); }
    }
}

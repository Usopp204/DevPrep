using System.Globalization;
using Microsoft.EntityFrameworkCore;
using StatisticsAPI.Data;
using StatisticsAPI.DTOs;
using StatisticsAPI.Helpers;
using StatisticsAPI.Models;

namespace StatisticsAPI.Services;

public interface IStatisticsQueryService
{
    Task<StatisticsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<TimeSeriesResult> GetTimeSeriesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<ManagementDashboardDto> GetDashboardAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public sealed class StatisticsQueryService(StatisticsDbContext db) : IStatisticsQueryService
{
    public async Task<StatisticsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var counters = await db.MetricCounters.AsNoTracking().ToListAsync(cancellationToken);
        var map = counters.ToDictionary(counter => counter.MetricKey, counter => counter.MetricValue, StringComparer.OrdinalIgnoreCase);

        long LongValue(string key) => map.TryGetValue(key, out var value) ? (long)value : 0L;
        decimal DecimalValue(string key) => map.TryGetValue(key, out var value) ? value : 0m;

        return new StatisticsOverviewDto
        {
            TotalUsers = LongValue(MetricKeys.TotalUsers),
            ActiveUsers = LongValue(MetricKeys.ActiveUsers),
            NewUsersThisMonth = LongValue(MetricKeys.NewUsersThisMonth),
            TotalSessions = LongValue(MetricKeys.TotalSessions),
            CompletedSessions = LongValue(MetricKeys.CompletedSessions),
            TotalRevenue = DecimalValue(MetricKeys.TotalRevenue),
            RevenueThisMonth = DecimalValue(MetricKeys.RevenueThisMonth),
            PaidOrders = LongValue(MetricKeys.PaidOrders),
            TotalFeedbacks = LongValue(MetricKeys.TotalFeedbacks),
            UpdatedAt = counters.Count > 0 ? counters.Max(counter => counter.UpdatedAt) : null
        };
    }

    public async Task<TimeSeriesResult> GetTimeSeriesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
            (from, to) = (to, from);

        var rows = await db.StatisticsSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.SnapshotDate >= from && snapshot.SnapshotDate <= to)
            .OrderBy(snapshot => snapshot.SnapshotDate)
            .ToListAsync(cancellationToken);

        var days = rows.Select(snapshot => new TimeSeriesPoint
        {
            Date = snapshot.SnapshotDate,
            NewUsers = snapshot.NewUsers,
            ActiveUsers = snapshot.ActiveUsers,
            InterviewSessions = snapshot.InterviewSessions,
            CompletedSessions = snapshot.CompletedSessions,
            Revenue = snapshot.Revenue,
            PaidOrders = snapshot.PaidOrders,
            NewFeedbacks = snapshot.NewFeedbacks
        }).ToList();

        return new TimeSeriesResult
        {
            From = from,
            To = to,
            TotalRevenue = days.Sum(day => day.Revenue),
            TotalPaidOrders = days.Sum(day => day.PaidOrders),
            TotalNewUsers = days.Sum(day => day.NewUsers),
            Days = days
        };
    }

    public async Task<ManagementDashboardDto> GetDashboardAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var result = NewDashboardResult(from, to);
        var requiredKeys = new[]
        {
            MetricKeys.DashboardGeneration,
            MetricKeys.DashboardTotalCandidates,
            MetricKeys.DashboardTotalInterviewSessions,
            MetricKeys.DashboardCompletedInterviewSessions,
            MetricKeys.DashboardTotalFeedbacks,
            MetricKeys.DashboardWindowFrom,
            MetricKeys.DashboardWindowTo
        };
        var counters = await db.MetricCounters.AsNoTracking()
            .Where(counter => requiredKeys.Contains(counter.MetricKey))
            .ToDictionaryAsync(counter => counter.MetricKey, cancellationToken);

        if (!counters.TryGetValue(MetricKeys.DashboardGeneration, out var generation) ||
            !TryGenerationCounter(counters, MetricKeys.DashboardTotalCandidates, generation.UpdatedAt, out var totalCandidates) ||
            !TryGenerationCounter(counters, MetricKeys.DashboardTotalInterviewSessions, generation.UpdatedAt, out var totalInterviewSessions) ||
            !TryGenerationCounter(counters, MetricKeys.DashboardCompletedInterviewSessions, generation.UpdatedAt, out var completedInterviewSessions) ||
            !TryGenerationCounter(counters, MetricKeys.DashboardTotalFeedbacks, generation.UpdatedAt, out var totalFeedbacks) ||
            !counters.TryGetValue(MetricKeys.DashboardWindowFrom, out var windowFromCounter) ||
            !counters.TryGetValue(MetricKeys.DashboardWindowTo, out var windowToCounter) ||
            windowFromCounter.UpdatedAt != generation.UpdatedAt ||
            windowToCounter.UpdatedAt != generation.UpdatedAt ||
            !TryDateKey(windowFromCounter.MetricValue, out var windowFrom) ||
            !TryDateKey(windowToCounter.MetricValue, out var windowTo))
        {
            return result;
        }

        result.LastUpdated = generation.UpdatedAt;
        result.Stale = VietnamTime.Now - generation.UpdatedAt > TimeSpan.FromMinutes(30);

        if (from < windowFrom || to > windowTo)
            return result;

        var generationDate = DateOnly.FromDateTime(generation.UpdatedAt);
        var monthFrom = new DateOnly(generationDate.Year, generationDate.Month, 1);
        if (monthFrom < windowFrom || generationDate > windowTo)
            return result;

        var rowsFrom = from < monthFrom ? from : monthFrom;
        var rowsTo = to > generationDate ? to : generationDate;
        var rows = await db.StatisticsSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.SnapshotDate >= rowsFrom && snapshot.SnapshotDate <= rowsTo)
            .OrderBy(snapshot => snapshot.SnapshotDate)
            .ToListAsync(cancellationToken);

        if (!ContainsCompleteRange(rows, rowsFrom, rowsTo))
            return result;

        var selectedRows = rows.Where(row => row.SnapshotDate >= from && row.SnapshotDate <= to).ToList();
        var monthRows = rows.Where(row => row.SnapshotDate >= monthFrom && row.SnapshotDate <= generationDate).ToList();
        if (!ContainsCompleteRange(selectedRows, from, to) ||
            !ContainsCompleteRange(monthRows, monthFrom, generationDate))
        {
            return result;
        }

        result.Available = true;
        result.TotalCandidates = totalCandidates;
        result.NewCandidatesThisMonth = monthRows.Sum(row => (long)row.NewUsers);
        result.TotalInterviewSessions = totalInterviewSessions;
        result.InterviewSessionsThisMonth = monthRows.Sum(row => (long)row.InterviewSessions);
        result.CompletedInterviewSessions = completedInterviewSessions;
        result.GrossVnpayRevenueThisMonth = monthRows.Sum(row => row.Revenue);
        result.TotalFeedbacks = totalFeedbacks;
        result.FeedbacksThisMonth = monthRows.Sum(row => (long)row.NewFeedbacks);
        result.SelectedRangeGrossVnpayRevenue = selectedRows.Sum(row => row.Revenue);
        result.SelectedRangePaidVnpayOrders = selectedRows.Sum(row => (long)row.PaidOrders);
        result.RevenueBuckets = BuildBuckets(selectedRows, from, to, result.Granularity);
        return result;
    }

    private static ManagementDashboardDto NewDashboardResult(DateOnly from, DateOnly to) => new()
    {
        From = from,
        To = to,
        Granularity = GetGranularity(to.DayNumber - from.DayNumber + 1)
    };

    private static string GetGranularity(int inclusiveDays)
    {
        if (inclusiveDays <= 31)
            return "Daily";
        if (inclusiveDays <= 180)
            return "Weekly";
        return "Monthly";
    }

    private static List<RevenueBucketDto> BuildBuckets(
        IReadOnlyCollection<StatisticsSnapshot> rows,
        DateOnly from,
        DateOnly to,
        string granularity)
    {
        var byDate = rows.ToDictionary(row => row.SnapshotDate);
        var buckets = new List<RevenueBucketDto>();

        if (granularity == "Daily")
        {
            for (var date = from; date <= to; date = date.AddDays(1))
                buckets.Add(CreateBucket(date, date, date.ToString("dd/MM", CultureInfo.InvariantCulture), byDate));
            return buckets;
        }

        if (granularity == "Weekly")
        {
            for (var start = from; start <= to; start = start.AddDays(7))
            {
                var end = start.AddDays(6);
                if (end > to)
                    end = to;
                buckets.Add(CreateBucket(start, end, RangeLabel(start, end), byDate));
            }

            return buckets;
        }

        var monthStart = from;
        while (monthStart <= to)
        {
            var calendarMonthEnd = new DateOnly(
                monthStart.Year,
                monthStart.Month,
                DateTime.DaysInMonth(monthStart.Year, monthStart.Month));
            var end = calendarMonthEnd < to ? calendarMonthEnd : to;
            buckets.Add(CreateBucket(
                monthStart,
                end,
                monthStart.ToString("MM/yyyy", CultureInfo.InvariantCulture),
                byDate));
            monthStart = end.AddDays(1);
        }

        return buckets;
    }

    private static RevenueBucketDto CreateBucket(
        DateOnly start,
        DateOnly end,
        string label,
        IReadOnlyDictionary<DateOnly, StatisticsSnapshot> byDate)
    {
        decimal revenue = 0;
        long orders = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var row = byDate[date];
            revenue += row.Revenue;
            orders += row.PaidOrders;
        }

        return new RevenueBucketDto
        {
            StartDate = start,
            EndDate = end,
            Label = label,
            Revenue = revenue,
            Orders = orders
        };
    }

    private static bool ContainsCompleteRange(
        IReadOnlyCollection<StatisticsSnapshot> rows,
        DateOnly from,
        DateOnly to)
    {
        var expectedDays = to.DayNumber - from.DayNumber + 1;
        if (rows.Count != expectedDays)
            return false;

        var dates = rows.Select(row => row.SnapshotDate).ToHashSet();
        if (dates.Count != expectedDays)
            return false;

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (!dates.Contains(date))
                return false;
        }

        return true;
    }

    private static bool TryGenerationCounter(
        IReadOnlyDictionary<string, MetricCounter> counters,
        string key,
        DateTime generationTime,
        out long value)
    {
        value = 0;
        if (!counters.TryGetValue(key, out var counter) ||
            counter.UpdatedAt != generationTime ||
            counter.MetricValue < 0 ||
            counter.MetricValue != decimal.Truncate(counter.MetricValue) ||
            counter.MetricValue > long.MaxValue)
        {
            return false;
        }

        value = (long)counter.MetricValue;
        return true;
    }

    private static string RangeLabel(DateOnly start, DateOnly end) =>
        $"{start:dd/MM}–{end:dd/MM}";

    private static bool TryDateKey(decimal value, out DateOnly date)
    {
        date = default;
        if (value != decimal.Truncate(value))
            return false;

        return DateOnly.TryParseExact(
            value.ToString("00000000", CultureInfo.InvariantCulture),
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }
}
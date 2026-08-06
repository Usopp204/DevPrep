using System.Globalization;
using System.Text.Json;
using WebClient.Models.Dashboard;
using WebClient.Models.Shared;

namespace WebClient.Services.Dashboard;

public interface IManagementDashboardService
{
    Task<ApiCallResult<ManagementDashboardData>> GetDashboardAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the Admin/Manager dashboard in memory from live domain-owned metrics.
/// </summary>
public sealed class ManagementDashboardService(
    IManagementDashboardSourceClient sourceClient,
    ILogger<ManagementDashboardService> logger) : IManagementDashboardService
{
    private const int MaximumSourceRangeDays = 366;

    public async Task<ApiCallResult<ManagementDashboardData>> GetDashboardAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (from > to)
            {
                return ApiCallResult<ManagementDashboardData>.Fail(
                    DashboardDateRange.OrderError,
                    StatusCodes.Status400BadRequest);
            }

            var inclusiveDays = to.DayNumber - from.DayNumber + 1;
            if (inclusiveDays > MaximumSourceRangeDays)
            {
                return ApiCallResult<ManagementDashboardData>.Fail(
                    DashboardDateRange.LengthError,
                    StatusCodes.Status400BadRequest);
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var monthFrom = new DateOnly(today.Year, today.Month, 1);

            DashboardSourceBundle selectedSources;
            DashboardSourceBundle monthSources;

            // In the normal UI range, one four-source request set covers both the selected
            // range and current-month KPI calculations. Older historical ranges remain supported
            // with a separate current-month request set rather than a statistics cache.
            var combinedFrom = from < monthFrom ? from : monthFrom;
            var combinedTo = to > today ? to : today;
            var combinedDays = combinedTo.DayNumber - combinedFrom.DayNumber + 1;

            if (combinedDays <= MaximumSourceRangeDays)
            {
                selectedSources = await LoadSourcesAsync(combinedFrom, combinedTo, cancellationToken);
                monthSources = selectedSources;
            }
            else
            {
                var selectedTask = LoadSourcesAsync(from, to, cancellationToken);
                var monthTask = LoadSourcesAsync(monthFrom, today, cancellationToken);
                await Task.WhenAll(selectedTask, monthTask);
                selectedSources = await selectedTask;
                monthSources = await monthTask;
            }

            var selectedRevenueDays = SliceDays(
                selectedSources.Revenue.Days,
                day => day.Date,
                from,
                to);
            var monthCandidateDays = SliceDays(
                monthSources.Candidates.Days,
                day => day.Date,
                monthFrom,
                today);
            var monthInterviewDays = SliceDays(
                monthSources.Interviews.Days,
                day => day.Date,
                monthFrom,
                today);
            var monthRevenueDays = SliceDays(
                monthSources.Revenue.Days,
                day => day.Date,
                monthFrom,
                today);
            var monthFeedbackDays = SliceDays(
                monthSources.Feedback.Days,
                day => day.Date,
                monthFrom,
                today);

            var granularity = DashboardDateRange.Granularity(inclusiveDays);
            var dashboard = new ManagementDashboardData
            {
                Available = true,
                LastUpdated = DateTime.UtcNow.AddHours(7),
                TotalCandidates = selectedSources.Candidates.TotalCandidates,
                NewCandidatesThisMonth = monthCandidateDays.Sum(day => day.Count),
                TotalInterviewSessions = selectedSources.Interviews.TotalInterviewSessions,
                InterviewSessionsThisMonth = monthInterviewDays.Sum(day => day.Count),
                CompletedInterviewSessions = selectedSources.Interviews.CompletedInterviewSessions,
                GrossVnpayRevenueThisMonth = monthRevenueDays.Sum(day => day.Revenue),
                TotalFeedbacks = selectedSources.Feedback.AllTimeTotalFeedbacks,
                FeedbacksThisMonth = monthFeedbackDays.Sum(day => day.TotalFeedbacks),
                From = from,
                To = to,
                Granularity = granularity,
                SelectedRangeGrossVnpayRevenue = selectedRevenueDays.Sum(day => day.Revenue),
                SelectedRangePaidVnpayOrders = selectedRevenueDays.Sum(day => day.Orders),
                RevenueBuckets = BuildBuckets(selectedRevenueDays, from, to, granularity)
            };

            return ApiCallResult<ManagementDashboardData>.Ok(dashboard, StatusCodes.Status200OK);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DashboardSourceException exception)
        {
            logger.LogWarning(
                exception,
                "Dashboard source request failed with status {StatusCode}.",
                exception.StatusCode);
            return ApiCallResult<ManagementDashboardData>.Fail(
                exception.Message,
                exception.StatusCode);
        }
        catch (TaskCanceledException exception)
        {
            logger.LogWarning(exception, "Dashboard source request timed out.");
            return ApiCallResult<ManagementDashboardData>.Fail(
                "Could not load dashboard statistics.",
                StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "A dashboard source is unreachable.");
            return ApiCallResult<ManagementDashboardData>.Fail(
                "Could not load dashboard statistics.",
                StatusCodes.Status502BadGateway);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "A dashboard source returned malformed JSON.");
            return ApiCallResult<ManagementDashboardData>.Fail(
                "Could not load dashboard statistics.",
                StatusCodes.Status502BadGateway);
        }
    }

    private async Task<DashboardSourceBundle> LoadSourcesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var candidatesTask = sourceClient.GetCandidateStatisticsAsync(from, to, cancellationToken);
        var interviewsTask = sourceClient.GetInterviewStatisticsAsync(from, to, cancellationToken);
        var revenueTask = sourceClient.GetRevenueStatisticsAsync(from, to, cancellationToken);
        var feedbackTask = sourceClient.GetFeedbackStatisticsAsync(from, to, cancellationToken);

        await Task.WhenAll(candidatesTask, interviewsTask, revenueTask, feedbackTask);

        return new DashboardSourceBundle(
            await candidatesTask,
            await interviewsTask,
            await revenueTask,
            await feedbackTask);
    }

    private static List<T> SliceDays<T>(
        IEnumerable<T> days,
        Func<T, DateOnly> dateSelector,
        DateOnly from,
        DateOnly to) =>
        days.Where(day => dateSelector(day) >= from && dateSelector(day) <= to)
            .OrderBy(dateSelector)
            .ToList();

    private static List<ManagementRevenueBucket> BuildBuckets(
        IReadOnlyCollection<DashboardRevenueDay> rows,
        DateOnly from,
        DateOnly to,
        string granularity)
    {
        var byDate = rows.ToDictionary(row => row.Date);
        var buckets = new List<ManagementRevenueBucket>();

        if (granularity == "Daily")
        {
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                buckets.Add(CreateBucket(
                    date,
                    date,
                    date.ToString("dd/MM", CultureInfo.InvariantCulture),
                    byDate));
            }

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

    private static ManagementRevenueBucket CreateBucket(
        DateOnly start,
        DateOnly end,
        string label,
        IReadOnlyDictionary<DateOnly, DashboardRevenueDay> byDate)
    {
        decimal revenue = 0;
        long orders = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var row = byDate[date];
            revenue += row.Revenue;
            orders += row.Orders;
        }

        return new ManagementRevenueBucket
        {
            StartDate = start,
            EndDate = end,
            Label = label,
            Revenue = revenue,
            Orders = orders
        };
    }

    private static string RangeLabel(DateOnly start, DateOnly end) =>
        $"{start:dd/MM}–{end:dd/MM}";

    private sealed record DashboardSourceBundle(
        CandidateDashboardSource Candidates,
        InterviewDashboardSource Interviews,
        RevenueDashboardSource Revenue,
        FeedbackDashboardSource Feedback);
}

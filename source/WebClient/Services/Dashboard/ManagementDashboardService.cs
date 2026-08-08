using System.Globalization;
using System.Net;
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

public sealed class ManagementDashboardService(
    IManagementDashboardSourceClient sourceClient,
    ILogger<ManagementDashboardService> logger) : IManagementDashboardService
{
    public async Task<ApiCallResult<ManagementDashboardData>> GetDashboardAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var candidateTask = sourceClient.GetCandidateStatisticsAsync(from, to, cancellationToken);
            var interviewTask = sourceClient.GetInterviewStatisticsAsync(from, to, cancellationToken);
            var revenueTask = sourceClient.GetRevenueAsync(from, to, cancellationToken);
            var feedbackTask = sourceClient.GetFeedbackStatisticsAsync(from, to, cancellationToken);

            await Task.WhenAll(candidateTask, interviewTask, revenueTask, feedbackTask);

            var candidate = await candidateTask;
            var interview = await interviewTask;
            var revenue = await revenueTask;
            var feedback = await feedbackTask;
            var granularity = DashboardDateRange.Granularity(to.DayNumber - from.DayNumber + 1);

            return ApiCallResult<ManagementDashboardData>.Ok(
                new ManagementDashboardData
                {
                    Available = true,
                    LastUpdated = DateTime.UtcNow.AddHours(7),
                    From = from,
                    To = to,
                    Granularity = granularity,
                    TotalCandidatesAsOf = candidate.TotalCandidatesAsOf,
                    NewCandidates = candidate.Days.Sum(day => day.Count),
                    TotalInterviewSessionsAsOf = interview.TotalInterviewSessionsAsOf,
                    CompletedInterviewSessionsAsOf = interview.CompletedInterviewSessionsAsOf,
                    InterviewSessionsCreated = interview.Days.Sum(day => day.CreatedCount),
                    InterviewSessionsCompleted = interview.Days.Sum(day => day.CompletedCount),
                    TotalSystemFeedbacksAsOf = feedback.TotalSystemFeedbacksAsOf,
                    TotalInterviewFeedbacksAsOf = feedback.TotalInterviewFeedbacksAsOf,
                    TotalFeedbacksAsOf = feedback.TotalFeedbacksAsOf,
                    NewSystemFeedbacks = feedback.Days.Sum(day => day.SystemFeedbacks),
                    NewInterviewFeedbacks = feedback.Days.Sum(day => day.InterviewFeedbacks),
                    NewFeedbacks = feedback.Days.Sum(day => day.TotalFeedbacks),
                    GrossVnpayRevenueAsOf = revenue.TotalRevenueAsOf,
                    PaidVnpayOrdersAsOf = revenue.TotalOrdersAsOf,
                    PeriodGrossVnpayRevenue = revenue.PeriodRevenue,
                    PeriodPaidVnpayOrders = revenue.PeriodOrders,
                    Buckets = BuildBuckets(candidate.Days, interview.Days, revenue.Days, feedback.Days, from, to, granularity)
                },
                StatusCodes.Status200OK);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException exception)
        {
            logger.LogWarning(exception, "Dashboard statistics request timed out.");
            return ApiCallResult<ManagementDashboardData>.Fail(
                "Could not load dashboard statistics.",
                StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Dashboard statistics are unreachable.");
            return ApiCallResult<ManagementDashboardData>.Fail(
                "Could not load dashboard statistics.",
                SourceFailureStatus(exception.StatusCode));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Dashboard statistics returned invalid source data.");
            return ApiCallResult<ManagementDashboardData>.Fail(
                "Could not load dashboard statistics.",
                StatusCodes.Status502BadGateway);
        }
    }

    private static int SourceFailureStatus(HttpStatusCode? statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => StatusCodes.Status400BadRequest,
        HttpStatusCode.Unauthorized => StatusCodes.Status401Unauthorized,
        HttpStatusCode.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status502BadGateway
    };

    private static List<ManagementDashboardBucket> BuildBuckets(
        IReadOnlyCollection<ManagementCountDay> candidateDays,
        IReadOnlyCollection<ManagementInterviewDay> interviewDays,
        IReadOnlyCollection<ManagementRevenueDay> revenueDays,
        IReadOnlyCollection<ManagementFeedbackDay> feedbackDays,
        DateOnly from,
        DateOnly to,
        string granularity)
    {
        var candidatesByDate = candidateDays.ToDictionary(day => day.Date);
        var interviewsByDate = interviewDays.ToDictionary(day => day.Date);
        var revenueByDate = revenueDays.ToDictionary(day => day.Date);
        var feedbackByDate = feedbackDays.ToDictionary(day => day.Date);
        var buckets = new List<ManagementDashboardBucket>();

        if (granularity == "Daily")
        {
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                buckets.Add(CreateBucket(
                    date,
                    date,
                    date.ToString("dd/MM", CultureInfo.InvariantCulture),
                    candidatesByDate,
                    interviewsByDate,
                    revenueByDate,
                    feedbackByDate));
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
                buckets.Add(CreateBucket(
                    start,
                    end,
                    $"{start:dd/MM}–{end:dd/MM}",
                    candidatesByDate,
                    interviewsByDate,
                    revenueByDate,
                    feedbackByDate));
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
                candidatesByDate,
                interviewsByDate,
                revenueByDate,
                feedbackByDate));
            monthStart = end.AddDays(1);
        }

        return buckets;
    }

    private static ManagementDashboardBucket CreateBucket(
        DateOnly start,
        DateOnly end,
        string label,
        IReadOnlyDictionary<DateOnly, ManagementCountDay> candidatesByDate,
        IReadOnlyDictionary<DateOnly, ManagementInterviewDay> interviewsByDate,
        IReadOnlyDictionary<DateOnly, ManagementRevenueDay> revenueByDate,
        IReadOnlyDictionary<DateOnly, ManagementFeedbackDay> feedbackByDate)
    {
        var bucket = new ManagementDashboardBucket
        {
            StartDate = start,
            EndDate = end,
            Label = label
        };

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            bucket.NewCandidates += candidatesByDate[date].Count;
            bucket.InterviewSessionsCreated += interviewsByDate[date].CreatedCount;
            bucket.InterviewSessionsCompleted += interviewsByDate[date].CompletedCount;
            bucket.GrossVnpayRevenue += revenueByDate[date].Revenue;
            bucket.PaidVnpayOrders += revenueByDate[date].Orders;
            bucket.SystemFeedbacks += feedbackByDate[date].SystemFeedbacks;
            bucket.InterviewFeedbacks += feedbackByDate[date].InterviewFeedbacks;
            bucket.TotalFeedbacks += feedbackByDate[date].TotalFeedbacks;
        }

        return bucket;
    }
}

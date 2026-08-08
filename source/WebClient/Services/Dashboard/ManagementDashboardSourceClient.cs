using System.Net.Http.Json;
using System.Text.Json;
using WebClient.Models.Dashboard;
using WebClient.Services.Shared;

namespace WebClient.Services.Dashboard;

public interface IManagementDashboardSourceClient
{
    Task<CandidateStatisticsSourceData> GetCandidateStatisticsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<InterviewDashboardSourceData> GetInterviewStatisticsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<RevenueDailySourceData> GetRevenueAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<FeedbackDashboardSourceData> GetFeedbackStatisticsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public sealed class ManagementDashboardSourceClient(IApiClient apiClient) : IManagementDashboardSourceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CandidateStatisticsSourceData> GetCandidateStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var envelope = await GetAsync<SourceEnvelope<CandidateStatisticsSourceData>>(
            $"api/admin/users/statistics/candidates?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            cancellationToken);
        var data = envelope.Success ? envelope.Data : null;
        ValidateRange(data?.From, data?.To, from, to, data?.Days.Select(day => day.Date), "candidate");

        var periodCandidates = data?.Days.Sum(day => day.Count);
        if (data is null ||
            data.TotalCandidatesAsOf < 0 ||
            data.Days.Any(day => day.Count < 0) ||
            periodCandidates > data.TotalCandidatesAsOf)
        {
            throw new InvalidOperationException("Candidate statistics response is invalid.");
        }

        return data;
    }

    public async Task<InterviewDashboardSourceData> GetInterviewStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var envelope = await GetAsync<SourceEnvelope<InterviewDashboardSourceData>>(
            $"api/admin/interviews/statistics/dashboard?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            cancellationToken);
        var data = envelope.Success ? envelope.Data : null;
        ValidateRange(data?.From, data?.To, from, to, data?.Days.Select(day => day.Date), "interview");

        var periodCreated = data?.Days.Sum(day => day.CreatedCount);
        var periodCompleted = data?.Days.Sum(day => day.CompletedCount);
        if (data is null ||
            data.TotalInterviewSessionsAsOf < 0 ||
            data.CompletedInterviewSessionsAsOf < 0 ||
            data.CompletedInterviewSessionsAsOf > data.TotalInterviewSessionsAsOf ||
            data.Days.Any(day => day.CreatedCount < 0 || day.CompletedCount < 0) ||
            periodCreated > data.TotalInterviewSessionsAsOf ||
            periodCompleted > data.CompletedInterviewSessionsAsOf)
        {
            throw new InvalidOperationException("Interview statistics response is invalid.");
        }

        return data;
    }

    public async Task<RevenueDailySourceData> GetRevenueAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var data = await GetAsync<RevenueDailySourceData>(
            $"api/payment/admin/payments/revenue-daily?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            cancellationToken);
        ValidateRange(data?.From, data?.To, from, to, data?.Days.Select(day => day.Date), "payment");
        if (data is null ||
            data.TotalRevenueAsOf < 0 ||
            data.TotalOrdersAsOf < 0 ||
            data.PeriodRevenue < 0 ||
            data.PeriodOrders < 0 ||
            data.Days.Any(day => day.Revenue < 0 || day.Orders < 0) ||
            data.PeriodRevenue != data.Days.Sum(day => day.Revenue) ||
            data.PeriodOrders != data.Days.Sum(day => day.Orders) ||
            data.PeriodRevenue > data.TotalRevenueAsOf ||
            data.PeriodOrders > data.TotalOrdersAsOf)
        {
            throw new InvalidOperationException("Payment statistics response is invalid.");
        }

        return data;
    }

    public async Task<FeedbackDashboardSourceData> GetFeedbackStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var envelope = await GetAsync<SourceEnvelope<FeedbackDashboardSourceData>>(
            $"api/admin/feedbacks/statistics/dashboard?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            cancellationToken);
        var data = envelope.Success ? envelope.Data : null;
        ValidateRange(data?.From, data?.To, from, to, data?.Days.Select(day => day.Date), "feedback");

        var periodSystem = data?.Days.Sum(day => day.SystemFeedbacks);
        var periodInterview = data?.Days.Sum(day => day.InterviewFeedbacks);
        var periodTotal = data?.Days.Sum(day => day.TotalFeedbacks);
        if (data is null ||
            data.TotalSystemFeedbacksAsOf < 0 ||
            data.TotalInterviewFeedbacksAsOf < 0 ||
            data.TotalFeedbacksAsOf < 0 ||
            data.TotalFeedbacksAsOf != data.TotalSystemFeedbacksAsOf + data.TotalInterviewFeedbacksAsOf ||
            data.Days.Any(day =>
                day.SystemFeedbacks < 0 ||
                day.InterviewFeedbacks < 0 ||
                day.TotalFeedbacks < 0 ||
                day.TotalFeedbacks != day.SystemFeedbacks + day.InterviewFeedbacks) ||
            periodTotal != periodSystem + periodInterview ||
            periodSystem > data.TotalSystemFeedbacksAsOf ||
            periodInterview > data.TotalInterviewFeedbacksAsOf ||
            periodTotal > data.TotalFeedbacksAsOf)
        {
            throw new InvalidOperationException("Feedback statistics response is invalid.");
        }

        return data;
    }

    private static void ValidateRange(
        DateOnly? actualFrom,
        DateOnly? actualTo,
        DateOnly expectedFrom,
        DateOnly expectedTo,
        IEnumerable<DateOnly>? dates,
        string sourceName)
    {
        var expectedDays = expectedTo.DayNumber - expectedFrom.DayNumber + 1;
        var dateList = dates?.ToList();
        var actualDates = dateList?.ToHashSet();
        if (actualFrom != expectedFrom ||
            actualTo != expectedTo ||
            dateList?.Count != expectedDays ||
            actualDates is null ||
            actualDates.Count != expectedDays)
        {
            throw new InvalidOperationException($"{sourceName} statistics range is incomplete.");
        }

        for (var date = expectedFrom; date <= expectedTo; date = date.AddDays(1))
        {
            if (!actualDates.Contains(date))
                throw new InvalidOperationException($"{sourceName} statistics range is incomplete.");
        }
    }

    private async Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        using var response = await apiClient.HttpClient.GetAsync(relativeUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Dashboard source request failed.", null, response.StatusCode);

        var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return data ?? throw new JsonException("Dashboard source returned an empty response.");
    }

    private sealed class SourceEnvelope<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
    }
}

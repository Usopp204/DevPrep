using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using StatisticsAPI.Services;

namespace StatisticsAPI.Clients;

public sealed record SourceCountDay(DateOnly Date, long Count);
public sealed record CandidateStatisticsSourceData(
    DateOnly From,
    DateOnly To,
    long TotalCandidates,
    List<SourceCountDay> Days);
public sealed record InterviewDashboardSourceData(
    DateOnly From,
    DateOnly To,
    long TotalInterviewSessions,
    long CompletedInterviewSessions,
    List<SourceCountDay> Days);
public sealed record SourceRevenueDay(DateOnly Date, decimal Revenue, long Orders);
public sealed record RevenueDailySourceData(
    DateOnly From,
    DateOnly To,
    decimal TotalRevenue,
    long TotalOrders,
    List<SourceRevenueDay> Days);
public sealed record SourceFeedbackDay(
    DateOnly Date,
    long SystemFeedbacks,
    long InterviewFeedbacks,
    long TotalFeedbacks);
public sealed record FeedbackDashboardSourceData(
    DateOnly From,
    DateOnly To,
    long AllTimeSystemFeedbacks,
    long AllTimeInterviewFeedbacks,
    long AllTimeTotalFeedbacks,
    List<SourceFeedbackDay> Days);

public interface ISourceMetricsClient
{
    Task<CandidateStatisticsSourceData> GetCandidateStatisticsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<InterviewDashboardSourceData> GetInterviewDashboardStatisticsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<RevenueDailySourceData> GetRevenueDailyAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<FeedbackDashboardSourceData> GetFeedbackDashboardStatisticsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public sealed class SourceMetricsClient(
    IHttpClientFactory httpClientFactory,
    IServiceTokenFactory tokenFactory) : ISourceMetricsClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string? _token;
    private string Token => _token ??= tokenFactory.CreateAdminToken();

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
        if (data is null || data.TotalCandidates < 0 || data.Days.Any(day => day.Count < 0))
            throw new InvalidOperationException("Candidate statistics response is invalid.");
        return data;
    }

    public async Task<InterviewDashboardSourceData> GetInterviewDashboardStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var envelope = await GetAsync<SourceEnvelope<InterviewDashboardSourceData>>(
            $"api/admin/interviews/statistics/dashboard?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            cancellationToken);
        var data = envelope.Success ? envelope.Data : null;
        ValidateRange(data?.From, data?.To, from, to, data?.Days.Select(day => day.Date), "interview");
        if (data is null ||
            data.TotalInterviewSessions < 0 ||
            data.CompletedInterviewSessions < 0 ||
            data.CompletedInterviewSessions > data.TotalInterviewSessions ||
            data.Days.Any(day => day.Count < 0))
        {
            throw new InvalidOperationException("Interview statistics response is invalid.");
        }

        return data;
    }

    public async Task<RevenueDailySourceData> GetRevenueDailyAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var data = await GetAsync<RevenueDailySourceData>(
            $"api/payment/admin/payments/revenue-daily?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            cancellationToken);
        ValidateRange(data?.From, data?.To, from, to, data?.Days.Select(day => day.Date), "payment");
        if (data is null ||
            data.TotalRevenue < 0 ||
            data.TotalOrders < 0 ||
            data.Days.Any(day => day.Revenue < 0 || day.Orders < 0))
        {
            throw new InvalidOperationException("Payment statistics response is invalid.");
        }

        if (data.TotalRevenue != data.Days.Sum(day => day.Revenue) ||
            data.TotalOrders != data.Days.Sum(day => day.Orders))
        {
            throw new InvalidOperationException("Payment statistics totals are inconsistent.");
        }

        return data;
    }

    public async Task<FeedbackDashboardSourceData> GetFeedbackDashboardStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var envelope = await GetAsync<SourceEnvelope<FeedbackDashboardSourceData>>(
            $"api/admin/feedbacks/statistics/dashboard?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            cancellationToken);
        var data = envelope.Success ? envelope.Data : null;
        ValidateRange(data?.From, data?.To, from, to, data?.Days.Select(day => day.Date), "feedback");
        if (data is null ||
            data.AllTimeSystemFeedbacks < 0 ||
            data.AllTimeInterviewFeedbacks < 0 ||
            data.AllTimeTotalFeedbacks < 0 ||
            data.AllTimeTotalFeedbacks != data.AllTimeSystemFeedbacks + data.AllTimeInterviewFeedbacks ||
            data.Days.Any(day =>
                day.SystemFeedbacks < 0 ||
                day.InterviewFeedbacks < 0 ||
                day.TotalFeedbacks < 0 ||
                day.TotalFeedbacks != day.SystemFeedbacks + day.InterviewFeedbacks))
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
        var client = httpClientFactory.CreateClient("gateway");
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
        return data ?? throw new JsonException("Statistics source returned an empty response.");
    }

    private sealed class SourceEnvelope<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
    }
}
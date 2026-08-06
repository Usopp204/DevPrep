using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WebClient.Models.Dashboard;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.Dashboard;

public interface IManagementDashboardSourceClient
{
    Task<CandidateDashboardSource> GetCandidateStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<InterviewDashboardSource> GetInterviewStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<RevenueDashboardSource> GetRevenueStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<FeedbackDashboardSource> GetFeedbackStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}

/// <summary>
/// Calls the domain-owned metric endpoints through ApiGateway using the current Admin/Manager JWT.
/// </summary>
public sealed class ManagementDashboardSourceClient(IApiClient apiClient)
    : IManagementDashboardSourceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CandidateDashboardSource> GetCandidateStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var data = await GetEnvelopeAsync<CandidateDashboardSource>(
            $"api/admin/users/statistics/candidates?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            "candidate",
            cancellationToken);

        ValidateRange(data.From, data.To, from, to, data.Days.Select(day => day.Date), "candidate");
        if (data.TotalCandidates < 0 || data.Days.Any(day => day.Count < 0))
            throw InvalidSource("Candidate statistics response is invalid.");

        return data;
    }

    public async Task<InterviewDashboardSource> GetInterviewStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var data = await GetEnvelopeAsync<InterviewDashboardSource>(
            $"api/admin/interviews/statistics/dashboard?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            "interview",
            cancellationToken);

        ValidateRange(data.From, data.To, from, to, data.Days.Select(day => day.Date), "interview");
        if (data.TotalInterviewSessions < 0 ||
            data.CompletedInterviewSessions < 0 ||
            data.CompletedInterviewSessions > data.TotalInterviewSessions ||
            data.Days.Any(day => day.Count < 0))
        {
            throw InvalidSource("Interview statistics response is invalid.");
        }

        return data;
    }

    public async Task<RevenueDashboardSource> GetRevenueStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var data = await GetRawAsync<RevenueDashboardSource>(
            $"api/payment/admin/payments/revenue-daily?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            "payment",
            cancellationToken);

        ValidateRange(data.From, data.To, from, to, data.Days.Select(day => day.Date), "payment");
        if (data.TotalRevenue < 0 ||
            data.TotalOrders < 0 ||
            data.Days.Any(day => day.Revenue < 0 || day.Orders < 0) ||
            data.TotalRevenue != data.Days.Sum(day => day.Revenue) ||
            data.TotalOrders != data.Days.Sum(day => day.Orders))
        {
            throw InvalidSource("Payment statistics response is invalid.");
        }

        return data;
    }

    public async Task<FeedbackDashboardSource> GetFeedbackStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var data = await GetEnvelopeAsync<FeedbackDashboardSource>(
            $"api/admin/feedbacks/statistics/dashboard?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            "feedback",
            cancellationToken);

        ValidateRange(data.From, data.To, from, to, data.Days.Select(day => day.Date), "feedback");
        if (data.AllTimeSystemFeedbacks < 0 ||
            data.AllTimeInterviewFeedbacks < 0 ||
            data.AllTimeTotalFeedbacks < 0 ||
            data.AllTimeTotalFeedbacks != data.AllTimeSystemFeedbacks + data.AllTimeInterviewFeedbacks ||
            data.Days.Any(day =>
                day.SystemFeedbacks < 0 ||
                day.InterviewFeedbacks < 0 ||
                day.TotalFeedbacks < 0 ||
                day.TotalFeedbacks != day.SystemFeedbacks + day.InterviewFeedbacks))
        {
            throw InvalidSource("Feedback statistics response is invalid.");
        }

        return data;
    }

    private async Task<T> GetEnvelopeAsync<T>(
        string relativeUrl,
        string sourceName,
        CancellationToken cancellationToken)
    {
        using var response = await apiClient.HttpClient.GetAsync(relativeUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateSourceExceptionAsync(response, sourceName, cancellationToken);

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions, cancellationToken);
        if (envelope?.Success != true || envelope.Data is null)
        {
            throw new DashboardSourceException(
                envelope?.Message ?? $"{sourceName} statistics are temporarily unavailable.",
                StatusCodes.Status502BadGateway);
        }

        return envelope.Data;
    }

    private async Task<T> GetRawAsync<T>(
        string relativeUrl,
        string sourceName,
        CancellationToken cancellationToken)
    {
        using var response = await apiClient.HttpClient.GetAsync(relativeUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateSourceExceptionAsync(response, sourceName, cancellationToken);

        var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return data ?? throw new JsonException($"{sourceName} statistics returned an empty response.");
    }

    private static async Task<DashboardSourceException> CreateSourceExceptionAsync(
        HttpResponseMessage response,
        string sourceName,
        CancellationToken cancellationToken)
    {
        string? message = null;
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.TryGetProperty("message", out var messageElement))
                message = messageElement.GetString();
        }
        catch (JsonException)
        {
            // Use the stable fallback when a source returns a non-JSON error page.
        }

        var statusCode = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => StatusCodes.Status400BadRequest,
            HttpStatusCode.Unauthorized => StatusCodes.Status401Unauthorized,
            HttpStatusCode.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status502BadGateway
        };

        return new DashboardSourceException(
            message ?? $"{sourceName} statistics are temporarily unavailable.",
            statusCode);
    }

    private static void ValidateRange(
        DateOnly actualFrom,
        DateOnly actualTo,
        DateOnly expectedFrom,
        DateOnly expectedTo,
        IEnumerable<DateOnly> dates,
        string sourceName)
    {
        var expectedDays = expectedTo.DayNumber - expectedFrom.DayNumber + 1;
        var dateList = dates.ToList();
        var actualDates = dateList.ToHashSet();

        if (actualFrom != expectedFrom ||
            actualTo != expectedTo ||
            dateList.Count != expectedDays ||
            actualDates.Count != expectedDays)
        {
            throw InvalidSource($"{sourceName} statistics range is incomplete.");
        }

        for (var date = expectedFrom; date <= expectedTo; date = date.AddDays(1))
        {
            if (!actualDates.Contains(date))
                throw InvalidSource($"{sourceName} statistics range is incomplete.");
        }
    }

    private static DashboardSourceException InvalidSource(string message) =>
        new(message, StatusCodes.Status502BadGateway);
}

public sealed class DashboardSourceException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

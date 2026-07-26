using System.Net.Http.Json;
using System.Text.Json;
using WebClient.Models.Feedback;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.Feedback;

public interface IFeedbackService
{
    Task<ApiCallResult<PagedResult<PublicSystemFeedbackDto>>> GetSystemReviewsAsync(
        int page, int pageSize, CancellationToken cancellationToken);

    Task<ApiCallResult<PagedResult<PublicAiFeedbackDto>>> GetAiReviewsAsync(
        int page, int pageSize, CancellationToken cancellationToken);

    Task<ApiCallResult<SystemFeedbackSubmissionStatusDto>> GetSystemSubmissionStatusAsync(
        CancellationToken cancellationToken);

    Task<ApiCallResult<CreatedSystemFeedbackDto>> SubmitSystemFeedbackAsync(
        SubmitSystemFeedbackRequest request, CancellationToken cancellationToken);

    Task<ApiCallResult<MyInterviewFeedbackDto>> GetMyInterviewFeedbackAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<ApiCallResult<CreatedInterviewFeedbackDto>> SubmitInterviewFeedbackAsync(
        SubmitInterviewFeedbackRequest request, CancellationToken cancellationToken);

    Task<ApiCallResult<CreatedFeedbackReportDto>> SubmitFeedbackReportAsync(
        string feedbackType,
        Guid feedbackId,
        CreateFeedbackReportRequest request,
        CancellationToken cancellationToken);

    Task<ApiCallResult<PagedResult<FeedbackReportListItemDto>>> GetFeedbackReportsAsync(
        bool manager,
        int page,
        int pageSize,
        string feedbackType,
        string status,
        string reasonCode,
        string keyword,
        CancellationToken cancellationToken);

    Task<ApiCallResult<FeedbackReportDetailsDto>> GetFeedbackReportDetailsAsync(
        bool manager, Guid reportId, CancellationToken cancellationToken);

    Task<ApiCallResult<FeedbackReportResolutionDto>> DismissFeedbackReportAsync(
        Guid reportId, ResolveFeedbackReportRequest request, CancellationToken cancellationToken);

    Task<ApiCallResult<FeedbackReportResolutionDto>> DeleteReportedFeedbackAsync(
        Guid reportId, ResolveFeedbackReportRequest request, CancellationToken cancellationToken);
}

public sealed class FeedbackService(IApiClient apiClient) : IFeedbackService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<ApiCallResult<PagedResult<PublicSystemFeedbackDto>>> GetSystemReviewsAsync(
        int page, int pageSize, CancellationToken cancellationToken) =>
        SendAsync<PagedResult<PublicSystemFeedbackDto>>(HttpMethod.Get,
            $"api/feedback/system/reviews?page={Math.Max(1, page)}&pageSize={Math.Clamp(pageSize, 1, 50)}",
            body: null,
            cancellationToken);

    public Task<ApiCallResult<PagedResult<PublicAiFeedbackDto>>> GetAiReviewsAsync(
        int page, int pageSize, CancellationToken cancellationToken) =>
        SendAsync<PagedResult<PublicAiFeedbackDto>>(HttpMethod.Get,
            $"api/feedback/ai/reviews?page={Math.Max(1, page)}&pageSize={Math.Clamp(pageSize, 1, 50)}",
            body: null,
            cancellationToken);

    public Task<ApiCallResult<SystemFeedbackSubmissionStatusDto>> GetSystemSubmissionStatusAsync(
        CancellationToken cancellationToken) =>
        SendAsync<SystemFeedbackSubmissionStatusDto>(HttpMethod.Get,
            "api/feedback/system/submission-status",
            body: null,
            cancellationToken);

    public Task<ApiCallResult<CreatedSystemFeedbackDto>> SubmitSystemFeedbackAsync(
        SubmitSystemFeedbackRequest request, CancellationToken cancellationToken) =>
        SendAsync<CreatedSystemFeedbackDto>(HttpMethod.Post,
            "api/feedback/system",
            request,
            cancellationToken);

    public Task<ApiCallResult<MyInterviewFeedbackDto>> GetMyInterviewFeedbackAsync(Guid sessionId, CancellationToken cancellationToken) =>
        SendAsync<MyInterviewFeedbackDto>(HttpMethod.Get,
            $"api/feedback/interview/sessions/{sessionId}/my-feedback",
            body: null,
            cancellationToken);
    public Task<ApiCallResult<CreatedInterviewFeedbackDto>> SubmitInterviewFeedbackAsync(
        SubmitInterviewFeedbackRequest request, CancellationToken cancellationToken) =>
        SendAsync<CreatedInterviewFeedbackDto>(HttpMethod.Post,
            "api/feedback/interview",
            request,
            cancellationToken);

    public Task<ApiCallResult<CreatedFeedbackReportDto>> SubmitFeedbackReportAsync(
        string feedbackType,
        Guid feedbackId,
        CreateFeedbackReportRequest request,
        CancellationToken cancellationToken)
    {
        var typeSegment = feedbackType switch
        {
            FeedbackReportRules.InterviewType => "ai",
            FeedbackReportRules.SystemType => "system",
            _ => null
        };
        if (typeSegment is null)
        {
            return Task.FromResult(ApiCallResult<CreatedFeedbackReportDto>.Fail(
                "Feedback type is invalid.",
                StatusCodes.Status400BadRequest));
        }

        return SendAsync<CreatedFeedbackReportDto>(
            HttpMethod.Post,
            $"api/feedback/{typeSegment}/reviews/{feedbackId}/reports",
            new { request.ReasonCode, request.Details },
            cancellationToken);
    }

    public Task<ApiCallResult<PagedResult<FeedbackReportListItemDto>>> GetFeedbackReportsAsync(
        bool manager,
        int page,
        int pageSize,
        string feedbackType,
        string status,
        string reasonCode,
        string keyword,
        CancellationToken cancellationToken)
    {
        var route = manager
            ? "api/admin/manager/feedback/reports"
            : "api/admin/feedback/reports";
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Clamp(pageSize, 1, 100)}",
            $"feedbackType={Uri.EscapeDataString(string.IsNullOrWhiteSpace(feedbackType) ? "All" : feedbackType)}",
            $"status={Uri.EscapeDataString(string.IsNullOrWhiteSpace(status) ? FeedbackReportRules.PendingStatus : status)}",
            $"reasonCode={Uri.EscapeDataString(string.IsNullOrWhiteSpace(reasonCode) ? "All" : reasonCode)}"
        };
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query.Add($"keyword={Uri.EscapeDataString(keyword.Trim())}");
        }

        return SendAsync<PagedResult<FeedbackReportListItemDto>>(
            HttpMethod.Get,
            $"{route}?{string.Join("&", query)}",
            body: null,
            cancellationToken);
    }

    public Task<ApiCallResult<FeedbackReportDetailsDto>> GetFeedbackReportDetailsAsync(
        bool manager,
        Guid reportId,
        CancellationToken cancellationToken) =>
        SendAsync<FeedbackReportDetailsDto>(
            HttpMethod.Get,
            manager
                ? $"api/admin/manager/feedback/reports/{reportId}"
                : $"api/admin/feedback/reports/{reportId}",
            body: null,
            cancellationToken);

    public Task<ApiCallResult<FeedbackReportResolutionDto>> DismissFeedbackReportAsync(
        Guid reportId,
        ResolveFeedbackReportRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<FeedbackReportResolutionDto>(
            HttpMethod.Patch,
            $"api/admin/feedback/reports/{reportId}/dismiss",
            request,
            cancellationToken);

    public Task<ApiCallResult<FeedbackReportResolutionDto>> DeleteReportedFeedbackAsync(
        Guid reportId,
        ResolveFeedbackReportRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<FeedbackReportResolutionDto>(
            HttpMethod.Patch,
            $"api/admin/feedback/reports/{reportId}/delete-feedback",
            request,
            cancellationToken);

    private async Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: JsonOptions);
            }

            using var response = await apiClient.HttpClient.SendAsync(request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                var error = ReadErrorMessage(raw) ?? ToDefaultMessage(statusCode);
                return ApiCallResult<T>.Fail(error, statusCode);
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return ApiCallResult<T>.Ok(default, statusCode);
            }

            ApiResponse<T>? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<ApiResponse<T>>(raw, JsonOptions);
            }
            catch (JsonException)
            {
                return ApiCallResult<T>.Fail($"Request to {path} returned an unreadable response ({statusCode}).", statusCode);
            }

            if (envelope is null || !envelope.Success)
            {
                return ApiCallResult<T>.Fail(envelope?.Message ?? ToDefaultMessage(statusCode), statusCode);
            }

            return ApiCallResult<T>.Ok(envelope.Data, statusCode);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<T>.Fail("The request timed out. Please try again.", StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<T>.Fail("Cannot reach the server. Please make sure the API Gateway is running.", StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static string? ReadErrorMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<ApiResponse<object>>(raw, JsonOptions);
            return string.IsNullOrWhiteSpace(envelope?.Message) ? null : envelope.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ToDefaultMessage(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity => "Please correct the feedback fields and try again.",
            StatusCodes.Status401Unauthorized => "Please log in to continue.",
            StatusCodes.Status403Forbidden => "You do not have permission to perform this action.",
            StatusCodes.Status404NotFound => "The requested item is no longer available.",
            StatusCodes.Status409Conflict => "Feedback already exists for this interview session.",
            StatusCodes.Status429TooManyRequests => "Too many requests. Please wait and try again.",
            StatusCodes.Status500InternalServerError or StatusCodes.Status502BadGateway or StatusCodes.Status503ServiceUnavailable => "The service is temporarily unavailable. Please try again later.",
            StatusCodes.Status504GatewayTimeout => "The request timed out. Please try again.",
            _ => "The request could not be completed. Please try again."
        };
}

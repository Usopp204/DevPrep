using System.Net.Http.Json;
using System.Text.Json;
using WebClient.Models.Evaluation;
using WebClient.Models.Interview;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.Evaluation;

public interface IEvaluationService
{
    EvaluationViewModel GetOverview();

    Task<(bool Success, ReportViewModel? Report, string? Error)> GetReportAsync(
        Guid sessionId, int durationActualSec, CancellationToken cancellationToken);

}

public sealed class EvaluationService(IApiClient apiClient) : IEvaluationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public EvaluationViewModel GetOverview()
    {
        _ = apiClient;

        return new EvaluationViewModel
        {
            ModuleName = "Evaluation & Reporting",
            Epic = "Evaluation & Reporting",
            Summary = "Open a completed session from Interview History to view its performance report."
        };
    }

    public async Task<(bool Success, ReportViewModel? Report, string? Error)> GetReportAsync(
        Guid sessionId, int durationActualSec, CancellationToken cancellationToken)
    {
        // The transcript gives session context (position/level/status) and the ordered question
        // list needed to label each per-question evaluation; without it there is no page to render.
        var transcript = await SendAsync<TranscriptResponse>(HttpMethod.Get,
            $"api/interview/sessions/{sessionId}/transcript",
            body: null,
            cancellationToken);
        if (!transcript.Success || transcript.Data is null)
            return (false, null, transcript.Error ?? "Could not load the session.");

        if (!string.Equals(transcript.Data.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            var terminalStateReason = string.Equals(transcript.Data.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                ? "This interview was cancelled, so no evaluation report was generated."
                : "This interview expired, so no evaluation report was generated.";
            return (true, new ReportViewModel
            {
                SessionId = sessionId,
                Position = transcript.Data.Position,
                Level = transcript.Data.Level,
                Status = transcript.Data.Status,
                Questions = transcript.Data.Questions
                    .OrderBy(q => q.OrderIndex)
                    .Select(q => new ReportQuestionViewModel
                    {
                        OrderIndex = q.OrderIndex,
                        Content = q.Content
                    })
                    .ToList(),
                EmptyReason = terminalStateReason
            }, null);
        }

        // Prefer an already-built report; only build a new one if none exists yet.
        var existingReport = await SendAsync<ReportResponse>(HttpMethod.Get,
            $"api/evaluation/sessions/{sessionId}/report",
            body: null,
            cancellationToken);

        ReportResponse? report = existingReport.Data;
        string? emptyReason = null;

        if (report is null)
        {
            var built = await SendAsync<ReportResponse>(HttpMethod.Post,
                $"api/evaluation/sessions/{sessionId}/report",
                new BuildReportRequest { DurationActualSec = durationActualSec },
                cancellationToken);

            if (built.Success)
            {
                report = built.Data;
            }
            else
            {
                // Typically "No evaluations found for this session." — render the page anyway
                // with the transcript info instead of failing outright.
                emptyReason = built.Error;
            }
        }

        var evaluations = await SendAsync<List<EvaluateAnswerResponse>>(HttpMethod.Get,
            $"api/evaluation/sessions/{sessionId}/answers",
            body: null,
            cancellationToken);
        var evaluationsBySessionQuestionId = (evaluations.Data ?? new List<EvaluateAnswerResponse>())
            .ToDictionary(e => e.SessionQuestionId);

        var questions = transcript.Data.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(q =>
            {
                evaluationsBySessionQuestionId.TryGetValue(q.SessionQuestionId, out var eval);
                return new ReportQuestionViewModel
                {
                    OrderIndex = q.OrderIndex,
                    Content = q.Content,
                    Score = eval?.Score,
                    AiFeedback = eval?.AiFeedback,
                    Strengths = eval?.Strengths
                };
            })
            .ToList();

        var viewModel = new ReportViewModel
        {
            SessionId = sessionId,
            Position = transcript.Data.Position,
            Level = transcript.Data.Level,
            Status = transcript.Data.Status,
            Report = report,
            Questions = questions,
            EmptyReason = report is null ? emptyReason : null
        };
        return (true, viewModel, null);
    }



    private async Task<(bool Success, T? Data, string? Error)> SendAsync<T>(
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
            if (!response.IsSuccessStatusCode)
            {
                return (false, default, ReadError(raw) ?? $"Request failed ({(int)response.StatusCode}).");
            }

            var envelope = string.IsNullOrWhiteSpace(raw)
                ? null
                : JsonSerializer.Deserialize<ApiResponse<T>>(raw, JsonOptions);

            if (envelope is null || !envelope.Success)
            {
                return (false, default, envelope?.Message ?? $"Request failed ({(int)response.StatusCode}).");
            }

            return (true, envelope.Data, null);
        }
        catch (Exception)
        {
            return (false, default, "Cannot reach the server. Please make sure the API Gateway is running.");
        }
    }

    private static string? ReadError(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<ApiResponse<object>>(raw, JsonOptions);
            return envelope?.Message;
        }
        catch (JsonException)
        {
            return raw.Length > 200 ? raw[..200] : raw;
        }
    }
}

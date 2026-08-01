using System.Globalization;
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
        Guid sessionId,
        int durationActualSec,
        CancellationToken cancellationToken);

    Task<ReportViewModel> GetSessionDetailsReportAsync(
        SessionHistoryItemDto? session,
        SessionChatLogResponse chatLog,
        SessionFeedbackEligibilityResponse? eligibility,
        CancellationToken cancellationToken);
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
        Guid sessionId,
        int durationActualSec,
        CancellationToken cancellationToken)
    {
        var transcript = await SendAsync<TranscriptResponse>(
            HttpMethod.Get,
            $"api/interview/sessions/{sessionId}/transcript",
            body: null,
            cancellationToken);
        if (transcript.StatusCode == StatusCodes.Status401Unauthorized)
            throw new AuthenticationExpiredException();
        if (!transcript.Success || transcript.Data is null)
            return (false, null, ToUserFacingError(transcript.StatusCode, transcript.Error, "Could not load the session."));

        var context = ReportSessionContext.FromTranscript(transcript.Data);
        if (!context.IsTerminal)
            return (false, null, "An Evaluation Report is available only after the interview has ended.");

        SessionFeedbackEligibilityResponse? eligibility = null;
        if (!context.IsExpired)
        {
            var eligibilityResult = await SendAsync<SessionFeedbackEligibilityResponse>(
                HttpMethod.Get,
                $"api/interview/sessions/{sessionId}/feedback-eligibility",
                body: null,
                cancellationToken);
            if (eligibilityResult.StatusCode == StatusCodes.Status401Unauthorized)
                throw new AuthenticationExpiredException();
            eligibility = eligibilityResult.Success ? eligibilityResult.Data : null;
        }

        var model = await LoadReportAsync(context, eligibility, durationActualSec, cancellationToken);
        return (true, model, null);
    }

    public Task<ReportViewModel> GetSessionDetailsReportAsync(
        SessionHistoryItemDto? session,
        SessionChatLogResponse chatLog,
        SessionFeedbackEligibilityResponse? eligibility,
        CancellationToken cancellationToken)
    {
        var context = ReportSessionContext.FromSessionDetails(session, chatLog);
        return LoadReportAsync(context, eligibility, durationActualSec: 0, cancellationToken);
    }

    private async Task<ReportViewModel> LoadReportAsync(
        ReportSessionContext context,
        SessionFeedbackEligibilityResponse? eligibility,
        int durationActualSec,
        CancellationToken cancellationToken)
    {
        var viewModel = CreateBaseViewModel(context, durationActualSec);
        if (context.IsExpired)
        {
            SetState(
                viewModel,
                EvaluationReportState.ExpiredNoResult,
                "Interview expired",
                "This interview expired without an evaluation result. No final score or grade is available for this session.");
            return viewModel;
        }

        if (eligibility is null
            || eligibility.SessionId != context.SessionId
            || !EqualsIgnoreCase(eligibility.Status, context.Status))
        {
            SetEvaluationUnavailable(viewModel);
            return viewModel;
        }

        if (!eligibility.HasSubstantiveAnswer)
        {
            SetState(
                viewModel,
                EvaluationReportState.NoEvaluatedAnswers,
                "No completed answers",
                "No completed answer was available for evaluation. No final score or grade is available for this session.");
            return viewModel;
        }

        if (!eligibility.CanGeneratePerformanceReport)
        {
            SetEvaluationUnavailable(viewModel);
            return viewModel;
        }

        var performanceResult = await SendAsync<PerformanceReportResponse>(
            HttpMethod.Get,
            $"api/evaluation/sessions/{context.SessionId}/performance",
            body: null,
            cancellationToken);
        if (performanceResult.StatusCode == StatusCodes.Status401Unauthorized)
            throw new AuthenticationExpiredException();
        if (!performanceResult.Success || performanceResult.Data is null)
        {
            SetEvaluationUnavailable(viewModel);
            return viewModel;
        }

        var performance = performanceResult.Data;
        if (!Enum.TryParse<EvaluationReportState>(performance.State, ignoreCase: true, out var state))
        {
            SetEvaluationUnavailable(viewModel);
            return viewModel;
        }

        if (state == EvaluationReportState.NoEvaluatedAnswers)
        {
            SetState(
                viewModel,
                state,
                "No completed answers",
                "No completed answer was available for evaluation. No final score or grade is available for this session.");
            return viewModel;
        }

        if (state == EvaluationReportState.ExpiredNoResult)
        {
            SetState(
                viewModel,
                state,
                "Interview expired",
                "This interview expired without an evaluation result. No final score or grade is available for this session.");
            return viewModel;
        }

        if (state == EvaluationReportState.EvaluationUnavailable)
        {
            SetEvaluationUnavailable(viewModel);
            return viewModel;
        }

        if (state == EvaluationReportState.EvaluationIncomplete)
        {
            await LoadQuestionEvaluationsAsync(viewModel, context, cancellationToken);
            SetState(
                viewModel,
                state,
                "Evaluation incomplete",
                "Some answers could not be evaluated before this interview ended, so a reliable overall score could not be calculated. No final score or grade is available for this session.");
            return viewModel;
        }

        if (state != EvaluationReportState.Scored || performance.OverallScore is null)
        {
            SetEvaluationUnavailable(viewModel);
            return viewModel;
        }

        var report = new ReportResponse
        {
            SessionId = performance.SessionId,
            OverallScore = performance.OverallScore.Value,
            TechnicalScore = performance.TechnicalScore,
            CommunicationScore = performance.CommunicationScore,
            ProblemSolvingScore = performance.ProblemSolvingScore,
            Summary = performance.Summary,
            Strengths = JoinStoredItems(performance.Strengths),
            Improvements = JoinStoredItems(performance.Improvements),
            Recommendations = performance.Recommendations,
            Grade = performance.Grade,
        };

        await LoadQuestionEvaluationsAsync(viewModel, context, cancellationToken);
        SetScoredReport(viewModel, report, viewModel.QuestionEvaluationsUnavailable);
        return viewModel;
    }

    private async Task LoadQuestionEvaluationsAsync(
        ReportViewModel viewModel,
        ReportSessionContext context,
        CancellationToken cancellationToken)
    {
        var result = await SendAsync<List<EvaluateAnswerResponse>>(
            HttpMethod.Get,
            $"api/evaluation/sessions/{context.SessionId}/answers",
            body: null,
            cancellationToken);
        if (result.StatusCode == StatusCodes.Status401Unauthorized)
            throw new AuthenticationExpiredException();
        if (!result.Success || result.Data is null)
        {
            viewModel.QuestionEvaluationsUnavailable = true;
            return;
        }

        viewModel.Questions = BuildQuestions(context.Questions, result.Data);
    }

    private static string? JoinStoredItems(IEnumerable<string> items)
    {
        var values = items.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        return values.Count == 0 ? null : string.Join("; ", values);
    }

    private static ReportViewModel CreateBaseViewModel(
        ReportSessionContext context,
        int durationActualSec) =>
        new()
        {
            SessionId = context.SessionId,
            DurationActualSec = durationActualSec,
            Position = context.Position,
            Level = context.Level,
            Status = context.Status,
            Questions = BuildQuestions(context.Questions, Array.Empty<EvaluateAnswerResponse>())
        };

    private static List<ReportQuestionViewModel> BuildQuestions(
        IReadOnlyList<ReportQuestionSource> questions,
        IEnumerable<EvaluateAnswerResponse> evaluations)
    {
        var evaluationsByQuestion = new Dictionary<Guid, EvaluateAnswerResponse>();
        foreach (var evaluation in evaluations)
            evaluationsByQuestion[evaluation.SessionQuestionId] = evaluation;

        return questions
            .OrderBy(question => question.OrderIndex)
            .Select(question =>
            {
                evaluationsByQuestion.TryGetValue(question.SessionQuestionId, out var matchedEvaluation);
                var evaluation = question.HasSubstantiveAnswer ? matchedEvaluation : null;
                return new ReportQuestionViewModel
                {
                    SessionQuestionId = question.SessionQuestionId,
                    OrderIndex = question.OrderIndex,
                    Content = question.Content,
                    Answer = question.Answer,
                    HasSubstantiveAnswer = question.HasSubstantiveAnswer,
                    Score = evaluation?.Score,
                    CriteriaScores = ParseCriteriaScores(evaluation?.CriteriaScoresJson),
                    AiFeedback = evaluation?.AiFeedback,
                    Strengths = evaluation?.Strengths,
                    AreasToImprove = DistinctAreasToImprove(
                        evaluation?.AreasToImprove,
                        evaluation?.AiFeedback)
                };
            })
            .ToList();
    }

    private static List<QuestionCriterionScoreViewModel> ParseCriteriaScores(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return new List<QuestionCriterionScoreViewModel>();

        try
        {
            var criteria = JsonSerializer.Deserialize<Dictionary<string, decimal>>(stored, JsonOptions);
            return criteria?
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new QuestionCriterionScoreViewModel
                {
                    Label = FormatCriterionLabel(pair.Key),
                    Score = pair.Value
                })
                .ToList() ?? new List<QuestionCriterionScoreViewModel>();
        }
        catch (JsonException)
        {
            return new List<QuestionCriterionScoreViewModel>();
        }
    }

    private static string FormatCriterionLabel(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        if (normalized == "knowledge")
            return "Technical";
        if (normalized == "communication")
            return "Communication";
        if (normalized == "problem_solving")
            return "Problem Solving";

        var words = normalized.Replace('_', ' ').Replace('-', ' ').Trim();
        return string.IsNullOrEmpty(words)
            ? "Criterion"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words);
    }

    private static string? DistinctAreasToImprove(string? areasToImprove, string? aiFeedback)
    {
        if (string.IsNullOrWhiteSpace(areasToImprove))
            return null;

        return string.Equals(
            areasToImprove.Trim(),
            aiFeedback?.Trim(),
            StringComparison.OrdinalIgnoreCase)
            ? null
            : areasToImprove;
    }

    private static bool EqualsIgnoreCase(string? actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static void SetEvaluationUnavailable(ReportViewModel viewModel) =>
        SetState(
            viewModel,
            EvaluationReportState.EvaluationUnavailable,
            "Evaluation unavailable",
            "This interview ended, but the submitted answers could not be evaluated. No final score or grade is available for this session.");

    private static void SetScoredReport(
        ReportViewModel viewModel,
        ReportResponse report,
        bool questionEvaluationsUnavailable)
    {
        viewModel.Report = report;
        viewModel.QuestionEvaluationsUnavailable = questionEvaluationsUnavailable;
        SetState(
            viewModel,
            EvaluationReportState.Scored,
            "Evaluation complete",
            "Your performance report is ready.");
    }

    private static void SetState(
        ReportViewModel viewModel,
        EvaluationReportState state,
        string title,
        string message)
    {
        viewModel.DisplayState = state;
        viewModel.StateTitle = title;
        viewModel.StateMessage = message;
    }

    private async Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
                request.Content = JsonContent.Create(body, options: JsonOptions);

            using var response = await apiClient.HttpClient.SendAsync(request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiCallResult<T>.Fail(
                    ReadError(raw) ?? $"Request failed ({(int)response.StatusCode}).",
                    (int)response.StatusCode);
            }

            var envelope = string.IsNullOrWhiteSpace(raw)
                ? null
                : JsonSerializer.Deserialize<ApiResponse<T>>(raw, JsonOptions);
            if (envelope is null || !envelope.Success)
            {
                return ApiCallResult<T>.Fail(
                    envelope?.Message ?? $"Request failed ({(int)response.StatusCode}).",
                    (int)response.StatusCode);
            }

            return ApiCallResult<T>.Ok(envelope.Data, (int)response.StatusCode);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<T>.Fail(
                "The request timed out. Please try again.",
                StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<T>.Fail(
                "Cannot reach the server. Please make sure the API Gateway is running.",
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (JsonException)
        {
            return ApiCallResult<T>.Fail(
                "The server returned an unreadable response.",
                StatusCodes.Status502BadGateway);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<T>.Fail(
                "The service is temporarily unavailable.",
                StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static string ToUserFacingError(int statusCode, string? backendMessage, string fallback) =>
        statusCode switch
        {
            StatusCodes.Status403Forbidden => "You do not have permission to view this interview.",
            StatusCodes.Status404NotFound => "This interview is no longer available.",
            StatusCodes.Status500InternalServerError or StatusCodes.Status502BadGateway or
                StatusCodes.Status503ServiceUnavailable or StatusCodes.Status504GatewayTimeout =>
                "The evaluation information is temporarily unavailable. Please try again later.",
            _ => string.IsNullOrWhiteSpace(backendMessage) ? fallback : backendMessage
        };

    private static string? ReadError(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            var envelope = JsonSerializer.Deserialize<ApiResponse<object>>(raw, JsonOptions);
            return envelope?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record ReportQuestionSource(
        Guid SessionQuestionId,
        int OrderIndex,
        string Content,
        string? Answer,
        bool HasSubstantiveAnswer);

    private sealed class ReportSessionContext
    {
        public Guid SessionId { get; init; }
        public string? Position { get; init; }
        public string Level { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public IReadOnlyList<ReportQuestionSource> Questions { get; init; } = Array.Empty<ReportQuestionSource>();
        public bool IsCompleted => EqualsIgnoreCase(Status, "Completed");
        public bool IsCancelled => EqualsIgnoreCase(Status, "Cancelled");
        public bool IsExpired => EqualsIgnoreCase(Status, "Expired");
        public bool IsTerminal => IsCompleted || IsCancelled || IsExpired;

        public static ReportSessionContext FromTranscript(TranscriptResponse transcript) =>
            new()
            {
                SessionId = transcript.SessionId,
                Position = transcript.Position,
                Level = transcript.Level,
                Status = transcript.Status,
                Questions = transcript.Questions
                    .Select(question =>
                    {
                        var answer = question.HasSubstantiveAnswer
                            ? transcript.ChatHistory
                                .Where(message =>
                                    message.SessionQuestionId == question.SessionQuestionId
                                    && EqualsIgnoreCase(message.SenderType, "User")
                                    && EqualsIgnoreCase(message.MessageType, "Answer")
                                    && !string.IsNullOrWhiteSpace(message.Content))
                                .OrderBy(message => message.SentAt)
                                .ThenBy(message => message.MessageId)
                                .LastOrDefault()
                                ?.Content
                            : null;

                        return new ReportQuestionSource(
                            question.SessionQuestionId,
                            question.OrderIndex,
                            question.Content,
                            answer,
                            question.HasSubstantiveAnswer);
                    })
                    .ToList()
            };

        public static ReportSessionContext FromSessionDetails(
            SessionHistoryItemDto? session,
            SessionChatLogResponse chatLog) =>
            new()
            {
                SessionId = chatLog.SessionId,
                Position = session?.Position,
                Level = session?.Level ?? string.Empty,
                Status = chatLog.SessionStatus,
                Questions = chatLog.Questions
                    .Select(question => new ReportQuestionSource(
                        question.SessionQuestionId,
                        question.QuestionIndex,
                        question.QuestionText,
                        question.HasSubstantiveAnswer
                            ? question.Messages
                                .Where(message => message.IsSubstantiveAnswer)
                                .OrderBy(message => message.SentAt)
                                .ThenBy(message => message.MessageId)
                                .LastOrDefault()
                                ?.Content
                            : null,
                        question.HasSubstantiveAnswer))
                    .ToList()
            };
    }
}
using System.Collections.Concurrent;
using System.Text.Json;
using EvaluationAPI.Clients;
using EvaluationAPI.DTOs.AiService;
using EvaluationAPI.DTOs.Evaluation;
using EvaluationAPI.Helpers;
using EvaluationAPI.Models;
using EvaluationAPI.Repositories.Interfaces;
using EvaluationAPI.Services.Interfaces;

namespace EvaluationAPI.Services;

public class EvaluationService : IEvaluationService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ReportGates = new();
    private readonly IEvaluationRepository _repo;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IInterviewApiClient _interviewApiClient;
    private readonly ILogger<EvaluationService> _logger;

    public EvaluationService(
        IEvaluationRepository repo,
        IAiServiceClient aiServiceClient,
        IInterviewApiClient interviewApiClient,
        ILogger<EvaluationService>? logger = null)
    {
        _repo = repo;
        _aiServiceClient = aiServiceClient;
        _interviewApiClient = interviewApiClient;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EvaluationService>.Instance;
    }

    public async Task<EvaluateAnswerResponse> EvaluateAnswerAsync(
        Guid sessionId,
        Guid answerId,
        EvaluateAnswerRequest req,
        CancellationToken ct)
    {
        if (sessionId == Guid.Empty
            || answerId == Guid.Empty
            || string.IsNullOrWhiteSpace(req.Question)
            || string.IsNullOrWhiteSpace(req.UserAnswer))
        {
            throw new EvaluationRequestException();
        }

        AnswerEvaluation? existing;
        try
        {
            existing = await _repo.GetBySessionQuestionAsync(sessionId, answerId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Existing answer evaluation lookup failed for session {SessionId}, question {SessionQuestionId}.",
                sessionId,
                answerId);
            throw new EvaluationPersistenceException(ex);
        }

        if (existing is not null)
        {
            _logger.LogInformation(
                "Existing answer evaluation returned for session {SessionId}, question {SessionQuestionId}, " +
                "evaluation {EvaluationId}.",
                sessionId,
                answerId,
                existing.EvaluationId);
            return ToResponse(existing);
        }

        AiEvaluateAnswerResponse response;
        try
        {
            response = await _aiServiceClient.EvaluateAnswerAsync(new AiEvaluateAnswerRequest
            {
                Question = req.Question,
                ReferenceAnswer = req.ReferenceAnswer,
                Category = req.Category,
                Difficulty = req.Difficulty,
                UserAnswer = req.UserAnswer,
                Followups = new List<object>()
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Answer grading dependency failed for session {SessionId}, question {SessionQuestionId}. " +
                "Category {FailureCategory}.",
                sessionId,
                answerId,
                "AiDependency");
            throw new EvaluationDependencyException(ex);
        }

        if (response.Score is null)
        {
            _logger.LogWarning(
                "Answer grading returned no score for session {SessionId}, question {SessionQuestionId}.",
                sessionId,
                answerId);
            throw new EvaluationDependencyException();
        }

        var evaluation = new AnswerEvaluation
        {
            EvaluationId = Guid.NewGuid(),
            SessionId = sessionId,
            SessionQuestionId = answerId,
            QuestionContent = req.Question,
            UserAnswer = req.UserAnswer,
            Score = (decimal)response.Score.Value,
            CriteriaScoresJson = JsonSerializer.Serialize(response.Criteria),
            Strengths = response.Praise,
            Weaknesses = response.SuggestedImprovement,
            AiFeedback = response.Analysis,
            ModelUsed = response.ModelUsed,
            GeneratedAt = VietnamTime.Now
        };

        try
        {
            await _repo.AddEvaluationAsync(evaluation, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Answer evaluation persistence failed for session {SessionId}, question {SessionQuestionId}, " +
                "evaluation {EvaluationId}.",
                sessionId,
                answerId,
                evaluation.EvaluationId);
            throw new EvaluationPersistenceException(ex);
        }

        return ToResponse(evaluation);
    }

    public async Task<PerformanceReportResponse> GetPerformanceReportAsync(
        Guid sessionId,
        string? bearerToken,
        CancellationToken ct)
    {
        var eligibility = await _interviewApiClient.GetSessionFeedbackEligibilityAsync(
            sessionId,
            bearerToken,
            ct);

        if (EqualsStatus(eligibility.Status, "Expired"))
            return StateOnly(sessionId, EvaluationLifecycleStates.ExpiredNoResult);

        if (!EqualsStatus(eligibility.Status, "Completed")
            && !EqualsStatus(eligibility.Status, "Cancelled"))
        {
            throw new SessionNotEligibleForPerformanceReportException(eligibility.Status);
        }

        if (!eligibility.HasSubstantiveAnswer)
            return StateOnly(sessionId, EvaluationLifecycleStates.NoEvaluatedAnswers);

        if (!eligibility.CanGeneratePerformanceReport)
            throw new SessionNotEligibleForPerformanceReportException(eligibility.Status);

        var context = await _interviewApiClient.GetSessionEvaluationContextAsync(
            sessionId,
            bearerToken,
            ct);
        if (!EqualsStatus(context.Status, eligibility.Status))
            throw new InterviewApiDependencyException();

        var substantiveQuestionIds = context.Questions
            .Where(question => question.HasSubstantiveAnswer)
            .Select(question => question.SessionQuestionId)
            .Where(questionId => questionId != Guid.Empty)
            .Distinct()
            .ToHashSet();

        if (substantiveQuestionIds.Count == 0)
            return StateOnly(sessionId, EvaluationLifecycleStates.NoEvaluatedAnswers);

        var persistedEvaluations = await _repo.GetEvaluationsBySessionAsync(sessionId, ct);
        var coveredEvaluations = persistedEvaluations
            .Where(evaluation => substantiveQuestionIds.Contains(evaluation.SessionQuestionId))
            .GroupBy(evaluation => evaluation.SessionQuestionId)
            .Select(group => group
                .OrderByDescending(evaluation => evaluation.GeneratedAt)
                .ThenByDescending(evaluation => evaluation.EvaluationId)
                .First())
            .ToList();

        if (coveredEvaluations.Count == 0)
            return StateOnly(sessionId, EvaluationLifecycleStates.EvaluationUnavailable);

        if (coveredEvaluations.Count < substantiveQuestionIds.Count)
            return StateOnly(sessionId, EvaluationLifecycleStates.EvaluationIncomplete);

        var aggregate = CalculateAggregate(coveredEvaluations);
        var report = await _repo.GetReportBySessionAsync(sessionId, ct);
        if (report is not null && AggregateRequiresRepair(report, aggregate))
        {
            await _repo.UpdateReportAggregateAsync(
                report.ReportId,
                aggregate.OverallScore,
                aggregate.TechnicalScore,
                aggregate.CommunicationScore,
                aggregate.ProblemSolvingScore,
                aggregate.Grade,
                ct);

            report.OverallScore = aggregate.OverallScore;
            report.TechnicalScore = aggregate.TechnicalScore;
            report.CommunicationScore = aggregate.CommunicationScore;
            report.ProblemSolvingScore = aggregate.ProblemSolvingScore;
            report.Grade = aggregate.Grade;
        }

        return ToPerformanceReportResponse(sessionId, report, aggregate);
    }

    public async Task<PerformanceReportResponse> EnsurePerformanceReportAsync(
        Guid sessionId,
        string? bearerToken,
        CancellationToken ct)
    {
        var eligibility = await _interviewApiClient.GetSessionFeedbackEligibilityAsync(
            sessionId,
            bearerToken,
            ct);
        if (!eligibility.CanGeneratePerformanceReport)
            throw new SessionNotEligibleForPerformanceReportException(eligibility.Status);

        var existing = await _repo.GetReportBySessionAsync(sessionId, ct);
        if (existing is not null)
            return await GetPerformanceReportAsync(sessionId, bearerToken, ct);

        var reportGate = ReportGates.GetOrAdd(sessionId, static _ => new SemaphoreSlim(1, 1));
        await reportGate.WaitAsync(ct);
        try
        {
            existing = await _repo.GetReportBySessionAsync(sessionId, ct);
            if (existing is not null)
                return await GetPerformanceReportAsync(sessionId, bearerToken, ct);

            var performance = await GetPerformanceReportAsync(sessionId, bearerToken, ct);
            if (!string.Equals(performance.State, EvaluationLifecycleStates.Scored, StringComparison.Ordinal))
                throw new AggregateReportNotReadyException(performance.State);

            var context = await _interviewApiClient.GetSessionEvaluationContextAsync(
                sessionId,
                bearerToken,
                ct);
            var substantiveQuestionIds = context.Questions
                .Where(question => question.HasSubstantiveAnswer)
                .Select(question => question.SessionQuestionId)
                .Where(questionId => questionId != Guid.Empty)
                .Distinct()
                .ToHashSet();
            var evaluations = (await _repo.GetEvaluationsBySessionAsync(sessionId, ct))
                .Where(evaluation => substantiveQuestionIds.Contains(evaluation.SessionQuestionId))
                .GroupBy(evaluation => evaluation.SessionQuestionId)
                .Select(group => group
                    .OrderByDescending(evaluation => evaluation.GeneratedAt)
                    .ThenByDescending(evaluation => evaluation.EvaluationId)
                    .First())
                .ToList();
            if (evaluations.Count != substantiveQuestionIds.Count)
                throw new AggregateReportNotReadyException(EvaluationLifecycleStates.EvaluationIncomplete);

            var aggregate = CalculateAggregate(evaluations);
            var durationSeconds = CalculateDurationSeconds(context);
            AiBuildReportResponse aiReport;
            try
            {
                var contextByQuestionId = context.Questions
                    .Where(question => question.SessionQuestionId != Guid.Empty)
                    .GroupBy(question => question.SessionQuestionId)
                    .ToDictionary(group => group.Key, group => group.First());

                aiReport = await _aiServiceClient.BuildReportAsync(new AiBuildReportRequest
                {
                    DurationActualSec = durationSeconds,
                    Evaluations = evaluations.Select(evaluation =>
                    {
                        contextByQuestionId.TryGetValue(
                            evaluation.SessionQuestionId,
                            out var questionContext);
                        return new AiAggregateEvaluationRow
                        {
                            Qid = evaluation.SessionQuestionId.ToString("D"),
                            Question = evaluation.QuestionContent ?? string.Empty,
                            UserAnswer = evaluation.UserAnswer ?? string.Empty,
                            Category = questionContext?.Category ?? string.Empty,
                            Difficulty = questionContext?.Difficulty ?? string.Empty,
                            Score = (double)evaluation.Score,
                            Criteria = (ParseCriteria(evaluation) ?? new Dictionary<string, decimal>())
                                .ToDictionary(item => item.Key, item => (double)item.Value),
                            Analysis = evaluation.AiFeedback ?? string.Empty,
                            Praise = evaluation.Strengths ?? string.Empty,
                            SuggestedImprovement = evaluation.Weaknesses ?? string.Empty
                        };
                    }).ToList()
                }, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new EvaluationDependencyException(ex);
            }

            var validatedSummary = ValidateAggregateResponse(aiReport, evaluations.Count);

            var generatedAt = VietnamTime.Now;
            var report = new FeedbackReport
            {
                ReportId = Guid.NewGuid(),
                SessionId = sessionId,
                OverallScore = aggregate.OverallScore,
                TechnicalScore = aggregate.TechnicalScore,
                CommunicationScore = aggregate.CommunicationScore,
                ProblemSolvingScore = aggregate.ProblemSolvingScore,
                Summary = validatedSummary.Praise,
                Strengths = JsonSerializer.Serialize(validatedSummary.Strengths),
                Improvements = JsonSerializer.Serialize(validatedSummary.AreasToImprove),
                Recommendations = validatedSummary.Advice,
                Grade = aggregate.Grade,
                GeneratedAt = generatedAt
            };
            var metrics = BuildMetrics(sessionId, aggregate, generatedAt);
            await _repo.CreateReportWithMetricsAsync(report, metrics, ct);
            return await GetPerformanceReportAsync(sessionId, bearerToken, ct);
        }
        finally
        {
            reportGate.Release();
        }
    }

    public async Task<List<EvaluateAnswerResponse>> GetAnswerEvaluationsAsync(        Guid sessionId,
        string? bearerToken,
        CancellationToken ct)
    {
        var eligibility = await _interviewApiClient.GetSessionFeedbackEligibilityAsync(sessionId, bearerToken, ct);
        if (!eligibility.CanSubmitInterviewFeedback)
            throw new SessionNotEligibleForPerformanceReportException(eligibility.Status);
        var evaluations = await _repo.GetEvaluationsBySessionAsync(sessionId, ct);
        return evaluations.Select(ToResponse).ToList();
    }


    private static ValidatedAggregateSummary ValidateAggregateResponse(
        AiBuildReportResponse response,
        int expectedQuestionCount)
    {
        if (response.NumQuestions != expectedQuestionCount || response.Summary is null)
            throw InvalidAggregateResponse();

        var praise = SanitizeText(response.Summary.Praise ?? string.Empty);
        var advice = SanitizeText(response.Summary.Advice ?? string.Empty);
        if (string.IsNullOrWhiteSpace(praise)
            || string.IsNullOrWhiteSpace(advice)
            || IsLegacyScoreBandSummary(praise))
        {
            throw InvalidAggregateResponse();
        }

        var strengths = ValidateSummaryItems(
            response.Summary.Strengths,
            minimumCount: 1);
        var areasToImprove = ValidateSummaryItems(
            response.Summary.AreasToImprove,
            minimumCount: 0);

        return new ValidatedAggregateSummary(
            praise,
            strengths,
            areasToImprove,
            advice);
    }

    private static List<string> ValidateSummaryItems(
        List<string>? values,
        int minimumCount)
    {
        if (values is null || values.Count < minimumCount || values.Count > 4)
            throw InvalidAggregateResponse();

        var normalized = values.Select(value => SanitizeText(value ?? string.Empty)).ToList();
        if (normalized.Any(string.IsNullOrWhiteSpace))
            throw InvalidAggregateResponse();

        return normalized;
    }

    private static bool IsLegacyScoreBandSummary(string value) =>
        string.Equals(value, "Excellent performance!", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "Good performance!", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "Keep practicing!", StringComparison.OrdinalIgnoreCase);

    private static EvaluationDependencyException InvalidAggregateResponse() =>
        new(new InvalidDataException("AI-Service returned an invalid aggregate report response."));
    private static AggregateResult CalculateAggregate(IReadOnlyCollection<AnswerEvaluation> evaluations)
    {
        var overallScore = Math.Round(evaluations.Average(evaluation => evaluation.Score));
        var criteria = evaluations
            .Select(ParseCriteria)
            .Where(values => values is not null)
            .Select(values => values!)
            .ToList();

        decimal? AxisAverage(string axis)
        {
            var values = criteria
                .Where(item => item.ContainsKey(axis))
                .Select(item => item[axis])
                .ToList();
            return values.Count == 0 ? null : Math.Round(values.Average());
        }

        return new AggregateResult(
            overallScore,
            AxisAverage("knowledge"),
            AxisAverage("communication"),
            AxisAverage("problem_solving"),
            GradeFor(overallScore));
    }

    private static Dictionary<string, decimal>? ParseCriteria(AnswerEvaluation evaluation)
    {
        if (string.IsNullOrWhiteSpace(evaluation.CriteriaScoresJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, decimal>>(evaluation.CriteriaScoresJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool AggregateRequiresRepair(FeedbackReport report, AggregateResult aggregate) =>
        report.OverallScore != aggregate.OverallScore
        || report.TechnicalScore != aggregate.TechnicalScore
        || report.CommunicationScore != aggregate.CommunicationScore
        || report.ProblemSolvingScore != aggregate.ProblemSolvingScore
        || !string.Equals(report.Grade, aggregate.Grade, StringComparison.Ordinal);

    private static string GradeFor(decimal overallScore)
    {
        if (overallScore >= 85)
            return "A";
        if (overallScore >= 70)
            return "B";
        return overallScore >= 55 ? "C" : "D";
    }

    private static PerformanceReportResponse StateOnly(Guid sessionId, string state) => new()
    {
        State = state,
        SessionId = sessionId
    };

    private static PerformanceReportResponse ToPerformanceReportResponse(
        Guid sessionId,
        FeedbackReport? report,
        AggregateResult aggregate) => new()
        {
            State = EvaluationLifecycleStates.Scored,
            SessionId = sessionId,
            HasPersistedReport = report is not null,
            OverallScore = aggregate.OverallScore,
            TechnicalScore = aggregate.TechnicalScore,
            CommunicationScore = aggregate.CommunicationScore,
            ProblemSolvingScore = aggregate.ProblemSolvingScore,
            Summary = report?.Summary,
            Strengths = ParseStoredStringCollection(report?.Strengths),
            Improvements = ParseStoredStringCollection(report?.Improvements),
            Recommendations = report?.Recommendations,
            Grade = aggregate.Grade,
        };

    private static int CalculateDurationSeconds(SessionEvaluationContextResult context)
    {
        if (context.StartedAt is null || context.EndedAt is null)
            return 0;

        var elapsed = (context.EndedAt.Value - context.StartedAt.Value).TotalSeconds
            - Math.Max(0, context.TotalPausedSeconds);
        return Math.Max(0, (int)Math.Round(elapsed));
    }

    private static List<PerformanceMetric> BuildMetrics(
        Guid sessionId,
        AggregateResult aggregate,
        DateTime recordedAt)
    {
        var values = new List<(string Name, decimal? Value)>
        {
            ("OverallScore", aggregate.OverallScore),
            ("TechnicalScore", aggregate.TechnicalScore),
            ("CommunicationScore", aggregate.CommunicationScore),
            ("ProblemSolvingScore", aggregate.ProblemSolvingScore)
        };
        return values
            .Where(item => item.Value.HasValue)
            .Select(item => new PerformanceMetric
            {
                MetricId = Guid.NewGuid(),
                SessionId = sessionId,
                MetricName = item.Name,
                MetricValue = item.Value!.Value,
                RecordedAt = recordedAt
            })
            .ToList();
    }

    private static EvaluateAnswerResponse ToResponse(AnswerEvaluation evaluation) => new()    {
        EvaluationId = evaluation.EvaluationId,
        SessionId = evaluation.SessionId,
        SessionQuestionId = evaluation.SessionQuestionId,
        Score = evaluation.Score,
        CriteriaScoresJson = evaluation.CriteriaScoresJson,
        Strengths = evaluation.Strengths,
        AiFeedback = evaluation.AiFeedback,
        AreasToImprove = evaluation.Weaknesses,
        ModelUsed = evaluation.ModelUsed
    };

    private static bool EqualsStatus(string? actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    public static List<string> ParseStoredStringCollection(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return new List<string>();

        var trimmed = stored.Trim();
        if (trimmed.StartsWith("[") || trimmed.StartsWith("{") || trimmed.StartsWith("\""))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                return ExtractJsonStrings(document.RootElement)
                    .Select(SanitizeText)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct()
                    .ToList();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }

        return SplitLegacyText(trimmed);
    }

    private static IEnumerable<string> ExtractJsonStrings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    yield return value;
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var extracted in ExtractJsonStrings(item))
                        yield return extracted;
                }
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var extracted in ExtractJsonStrings(property.Value))
                        yield return extracted;
                }
                break;
        }
    }

    private static List<string> SplitLegacyText(string value) =>
        value.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SanitizeText)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

    private static string SanitizeText(string value) =>
        value.Replace("\r", " ").Replace("\n", " ").Trim();

    private sealed record ValidatedAggregateSummary(
        string Praise,
        List<string> Strengths,
        List<string> AreasToImprove,
        string Advice);

    private sealed record AggregateResult(
        decimal OverallScore,
        decimal? TechnicalScore,
        decimal? CommunicationScore,
        decimal? ProblemSolvingScore,
        string Grade);
}
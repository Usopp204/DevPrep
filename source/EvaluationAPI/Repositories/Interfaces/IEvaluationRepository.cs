using EvaluationAPI.Models;

namespace EvaluationAPI.Repositories.Interfaces;

public interface IEvaluationRepository
{
    Task<AnswerEvaluation> AddEvaluationAsync(AnswerEvaluation eval, CancellationToken ct);
    Task<AnswerEvaluation?> GetBySessionQuestionAsync(Guid sessionId, Guid sessionQuestionId, CancellationToken ct);
    Task<List<AnswerEvaluation>> GetEvaluationsBySessionAsync(Guid sessionId, CancellationToken ct);
    Task<FeedbackReport?> GetReportBySessionAsync(Guid sessionId, CancellationToken ct);
    Task<FeedbackReport> CreateReportWithMetricsAsync(
        FeedbackReport report,
        IReadOnlyCollection<PerformanceMetric> metrics,
        CancellationToken ct);
    Task UpdateReportAggregateAsync(
        Guid reportId,
        decimal overallScore,
        decimal? technicalScore,
        decimal? communicationScore,
        decimal? problemSolvingScore,
        string grade,
        CancellationToken ct);
}
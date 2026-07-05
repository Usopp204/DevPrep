using EvaluationAPI.Models;

namespace EvaluationAPI.Repositories.Interfaces;

public interface IEvaluationRepository
{
    Task<AnswerEvaluation> AddEvaluationAsync(AnswerEvaluation eval, CancellationToken ct);
    Task<AnswerEvaluation?> GetBySessionQuestionAsync(Guid sessionId, Guid sessionQuestionId, CancellationToken ct);
    Task<List<AnswerEvaluation>> GetEvaluationsBySessionAsync(Guid sessionId, CancellationToken ct);
    Task<FeedbackReport> AddReportAsync(FeedbackReport report, CancellationToken ct);
    Task AddMetricsAsync(List<PerformanceMetric> metrics, CancellationToken ct);
    Task<bool> ReportExistsAsync(Guid sessionId, CancellationToken ct);
    Task<FeedbackReport?> GetReportBySessionAsync(Guid sessionId, CancellationToken ct);
}

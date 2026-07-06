using EvaluationAPI.DTOs.Evaluation;

namespace EvaluationAPI.Services.Interfaces;

public interface IEvaluationService
{
    Task<EvaluateAnswerResponse> EvaluateAnswerAsync(Guid sessionId, Guid answerId, EvaluateAnswerRequest req, CancellationToken ct);
    Task<ReportResponse> BuildReportAsync(Guid sessionId, BuildReportRequest req, CancellationToken ct);
    Task<ReportResponse?> GetExistingReportAsync(Guid sessionId, CancellationToken ct);
    Task<List<EvaluateAnswerResponse>> GetAnswerEvaluationsAsync(Guid sessionId, CancellationToken ct);
}

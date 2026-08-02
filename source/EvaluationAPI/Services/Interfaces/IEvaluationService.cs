using EvaluationAPI.DTOs.Evaluation;

namespace EvaluationAPI.Services.Interfaces;

public interface IEvaluationService
{
    Task<EvaluateAnswerResponse> EvaluateAnswerAsync(
        Guid sessionId,
        Guid answerId,
        EvaluateAnswerRequest req,
        CancellationToken ct);

    Task<PerformanceReportResponse> GetPerformanceReportAsync(
        Guid sessionId,
        string? bearerToken,
        CancellationToken ct);

    Task<PerformanceReportResponse> EnsurePerformanceReportAsync(
        Guid sessionId,
        string? bearerToken,
        CancellationToken ct);

    Task<List<EvaluateAnswerResponse>> GetAnswerEvaluationsAsync(
        Guid sessionId,
        string? bearerToken,
        CancellationToken ct);
}
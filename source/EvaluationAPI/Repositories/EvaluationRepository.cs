using EvaluationAPI.Data;
using EvaluationAPI.Models;
using EvaluationAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EvaluationAPI.Repositories;

public class EvaluationRepository : IEvaluationRepository
{
    private readonly EvaluationDbContext _db;

    public EvaluationRepository(EvaluationDbContext db)
    {
        _db = db;
    }

    public async Task<AnswerEvaluation> AddEvaluationAsync(AnswerEvaluation eval, CancellationToken ct)
    {
        _db.AnswerEvaluations.Add(eval);
        await _db.SaveChangesAsync(ct);
        return eval;
    }

    // Existing evaluation for one answer, if any (used to avoid double-grading a question).
    public Task<AnswerEvaluation?> GetBySessionQuestionAsync(
        Guid sessionId, Guid sessionQuestionId, CancellationToken ct) =>
        _db.AnswerEvaluations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.SessionId == sessionId && e.SessionQuestionId == sessionQuestionId, ct);

    public Task<List<AnswerEvaluation>> GetEvaluationsBySessionAsync(Guid sessionId, CancellationToken ct) =>
        _db.AnswerEvaluations
            .Where(e => e.SessionId == sessionId)
            .OrderBy(e => e.GeneratedAt)
            .ToListAsync(ct);

    public async Task<FeedbackReport> AddReportAsync(FeedbackReport report, CancellationToken ct)
    {
        _db.FeedbackReports.Add(report);
        await _db.SaveChangesAsync(ct);
        return report;
    }

    public async Task AddMetricsAsync(List<PerformanceMetric> metrics, CancellationToken ct)
    {
        _db.PerformanceMetrics.AddRange(metrics);
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> ReportExistsAsync(Guid sessionId, CancellationToken ct) =>
        _db.FeedbackReports.AnyAsync(r => r.SessionId == sessionId, ct);

    public Task<FeedbackReport?> GetReportBySessionAsync(Guid sessionId, CancellationToken ct) =>
        _db.FeedbackReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.SessionId == sessionId, ct);
}

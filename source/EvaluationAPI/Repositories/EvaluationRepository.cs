using EvaluationAPI.Data;
using EvaluationAPI.Models;
using EvaluationAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Data;

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

    public Task<AnswerEvaluation?> GetBySessionQuestionAsync(
        Guid sessionId, Guid sessionQuestionId, CancellationToken ct) =>
        _db.AnswerEvaluations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.SessionId == sessionId && e.SessionQuestionId == sessionQuestionId, ct);

    public Task<List<AnswerEvaluation>> GetEvaluationsBySessionAsync(Guid sessionId, CancellationToken ct) =>
        _db.AnswerEvaluations
            .AsNoTracking()
            .Where(e => e.SessionId == sessionId)
            .OrderBy(e => e.GeneratedAt)
            .ToListAsync(ct);

    public Task<FeedbackReport?> GetReportBySessionAsync(Guid sessionId, CancellationToken ct) =>
        _db.FeedbackReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.SessionId == sessionId, ct);

    public async Task<FeedbackReport> CreateReportWithMetricsAsync(
        FeedbackReport report,
        IReadOnlyCollection<PerformanceMetric> metrics,
        CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct);

        var existing = await _db.FeedbackReports
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.SessionId == report.SessionId, ct);
        if (existing is not null)
        {
            await transaction.CommitAsync(ct);
            return existing;
        }

        _db.FeedbackReports.Add(report);
        _db.PerformanceMetrics.AddRange(metrics);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return report;
    }

    public async Task UpdateReportAggregateAsync(
        Guid reportId,
        decimal overallScore,
        decimal? technicalScore,
        decimal? communicationScore,
        decimal? problemSolvingScore,
        string grade,
        CancellationToken ct)
    {
        var report = await _db.FeedbackReports
            .FirstOrDefaultAsync(item => item.ReportId == reportId, ct)
            ?? throw new InvalidOperationException("The performance report no longer exists.");

        report.OverallScore = overallScore;
        report.TechnicalScore = technicalScore;
        report.CommunicationScore = communicationScore;
        report.ProblemSolvingScore = problemSolvingScore;
        report.Grade = grade;
        await _db.SaveChangesAsync(ct);
    }
}
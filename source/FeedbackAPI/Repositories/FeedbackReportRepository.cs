using System.Data;
using FeedbackAPI.Models;
using FeedbackAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FeedbackAPI.Repositories;

public sealed class FeedbackReportRepository(FeedbackDbContext db) : IFeedbackReportRepository
{
    public IQueryable<FeedbackReport> Query() =>
        db.FeedbackReports
            .AsNoTracking()
            .Include(report => report.InterviewFeedback)
            .Include(report => report.SystemFeedback);

    public IQueryable<FeedbackReport> TrackingQuery() =>
        db.FeedbackReports
            .Include(report => report.InterviewFeedback)
            .Include(report => report.SystemFeedback);

    public Task<SystemFeedback?> GetSystemFeedbackAsync(
        Guid feedbackId,
        bool tracking,
        CancellationToken ct = default)
    {
        IQueryable<SystemFeedback> query = db.SystemFeedbacks;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(feedback => feedback.FeedbackId == feedbackId, ct);
    }

    public Task<bool> ExistsSystemAsync(
        Guid feedbackId,
        Guid reporterUserId,
        CancellationToken ct = default) =>
        db.FeedbackReports.AsNoTracking().AnyAsync(
            report => report.ReporterUserId == reporterUserId
                && report.SystemFeedbackId == feedbackId,
            ct);

    public async Task<IReadOnlyDictionary<Guid, FeedbackReportViewerState>> GetSystemViewerStatesAsync(
        IReadOnlyCollection<Guid> feedbackIds,
        Guid viewerUserId,
        CancellationToken ct = default)
    {
        if (feedbackIds.Count == 0)
        {
            return new Dictionary<Guid, FeedbackReportViewerState>();
        }

        var reportedIds = await db.FeedbackReports.AsNoTracking()
            .Where(report => report.ReporterUserId == viewerUserId
                && report.SystemFeedbackId.HasValue
                && feedbackIds.Contains(report.SystemFeedbackId.Value))
            .Select(report => report.SystemFeedbackId!.Value)
            .ToListAsync(ct);
        var reported = reportedIds.ToHashSet();
        return await db.SystemFeedbacks.AsNoTracking()
            .Where(feedback => feedbackIds.Contains(feedback.FeedbackId))
            .ToDictionaryAsync(
                feedback => feedback.FeedbackId,
                feedback => new FeedbackReportViewerState(
                    feedback.FeedbackId,
                    feedback.UserId == viewerUserId,
                    reported.Contains(feedback.FeedbackId)),
                ct);
    }

    public async Task AddAsync(FeedbackReport report, CancellationToken ct = default) =>
        await db.FeedbackReports.AddAsync(report, ct);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task<IDbContextTransaction?> BeginSerializableTransactionAsync(CancellationToken ct = default)
    {
        if (!db.Database.IsRelational())
        {
            return null;
        }

        return await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    }
}

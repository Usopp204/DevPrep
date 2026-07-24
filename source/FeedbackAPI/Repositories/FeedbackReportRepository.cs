using System.Data;
using FeedbackAPI.Models;
using FeedbackAPI.Repositories.Interfaces;
using FeedbackAPI.Validation;
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

    public Task<InterviewFeedback?> GetInterviewFeedbackAsync(
        Guid feedbackId,
        bool tracking,
        CancellationToken ct = default)
    {
        IQueryable<InterviewFeedback> query = db.InterviewFeedbacks;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(
            feedback => feedback.InterviewFeedbackId == feedbackId,
            ct);
    }

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

    public Task<bool> ExistsAsync(
        string feedbackType,
        Guid feedbackId,
        Guid reporterUserId,
        CancellationToken ct = default) =>
        db.FeedbackReports.AsNoTracking().AnyAsync(
            report => report.ReporterUserId == reporterUserId
                && report.FeedbackType == feedbackType
                && (feedbackType == FeedbackReportType.Interview
                    ? report.InterviewFeedbackId == feedbackId
                    : report.SystemFeedbackId == feedbackId),
            ct);

    public async Task<IReadOnlyDictionary<Guid, FeedbackReportViewerState>> GetViewerStatesAsync(
        string feedbackType,
        IReadOnlyCollection<Guid> feedbackIds,
        Guid viewerUserId,
        CancellationToken ct = default)
    {
        if (feedbackIds.Count == 0)
        {
            return new Dictionary<Guid, FeedbackReportViewerState>();
        }

        var reportedIds = feedbackType == FeedbackReportType.Interview
            ? await db.FeedbackReports.AsNoTracking()
                .Where(report => report.ReporterUserId == viewerUserId
                    && report.InterviewFeedbackId.HasValue
                    && feedbackIds.Contains(report.InterviewFeedbackId.Value))
                .Select(report => report.InterviewFeedbackId!.Value)
                .ToListAsync(ct)
            : await db.FeedbackReports.AsNoTracking()
                .Where(report => report.ReporterUserId == viewerUserId
                    && report.SystemFeedbackId.HasValue
                    && feedbackIds.Contains(report.SystemFeedbackId.Value))
                .Select(report => report.SystemFeedbackId!.Value)
                .ToListAsync(ct);

        var reported = reportedIds.ToHashSet();
        if (feedbackType == FeedbackReportType.Interview)
        {
            return await db.InterviewFeedbacks.AsNoTracking()
                .Where(feedback => feedbackIds.Contains(feedback.InterviewFeedbackId))
                .ToDictionaryAsync(
                    feedback => feedback.InterviewFeedbackId,
                    feedback => new FeedbackReportViewerState(
                        feedback.InterviewFeedbackId,
                        feedback.UserId == viewerUserId,
                        reported.Contains(feedback.InterviewFeedbackId)),
                    ct);
        }

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

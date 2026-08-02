using System.Data;
using FeedbackAPI.Models;
using FeedbackAPI.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Repositories;

public class SystemFeedbackRepository : ISystemFeedbackRepository
{
    private readonly FeedbackDbContext _db;

    public SystemFeedbackRepository(FeedbackDbContext db)
    {
        _db = db;
    }

    public IQueryable<SystemFeedback> Query() =>
        _db.SystemFeedbacks.AsNoTracking().Where(f => !f.IsDeleted);

    public IQueryable<SystemFeedback> QueryIncludingDeleted() =>
        _db.SystemFeedbacks.AsNoTracking();

    public IQueryable<SystemFeedback> PublicQuery() =>
        _db.SystemFeedbacks
            .AsNoTracking()
            .Where(f => !f.IsDeleted && f.IsVisible);

    public Task<SystemFeedback?> GetByIdAsync(Guid id, bool includeReplies = false, bool tracking = false, CancellationToken ct = default)
    {
        IQueryable<SystemFeedback> query = _db.SystemFeedbacks;
        if (includeReplies) query = query.Include(f => f.FeedbackReplies);
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(f => f.FeedbackId == id, ct);
    }

    public Task<SystemFeedback?> GetOwnedActiveByIdAsync(
        Guid feedbackId,
        Guid userId,
        bool tracking = false,
        CancellationToken ct = default)
    {
        IQueryable<SystemFeedback> query = _db.SystemFeedbacks;
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(
            f => f.FeedbackId == feedbackId && f.UserId == userId && !f.IsDeleted,
            ct);
    }

    public Task<int> CountByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.SystemFeedbacks.CountAsync(f => f.UserId == userId && !f.IsDeleted, ct);

    public Task<SystemFeedback?> GetLatestByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.SystemFeedbacks
            .AsNoTracking()
            .Where(f => f.UserId == userId && !f.IsDeleted)
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.FeedbackId)
            .FirstOrDefaultAsync(ct);


    public async Task ResolvePendingReportsAsync(Guid systemFeedbackId, CancellationToken ct = default)
    {
        var reports = await _db.FeedbackReports
            .Where(r => r.SystemFeedbackId == systemFeedbackId && r.Status == "Pending")
            .ToListAsync(ct);
        foreach (var r in reports)
        {
            r.Status = "ResolvedByFeedbackDeletion";
            r.ReviewedAt = Helpers.VietnamTime.Now;
        }
    }

    public async Task<bool> TryAddActiveAsync(SystemFeedback feedback, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await _db.SystemFeedbacks.AnyAsync(
                existing => existing.UserId == feedback.UserId && !existing.IsDeleted,
                ct))
        {
            return false;
        }

        await _db.SystemFeedbacks.AddAsync(feedback, ct);
        try
        {
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (Exception exception) when (IsSqlServerDeadlock(exception))
        {
            _db.Entry(feedback).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<FeedbackRestoreOutcome> RestoreAsync(
        Guid feedbackId,
        DateTime restoredAt,
        CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var feedback = await _db.SystemFeedbacks
            .FirstOrDefaultAsync(item => item.FeedbackId == feedbackId && item.IsDeleted, ct);
        if (feedback is null)
        {
            return FeedbackRestoreOutcome.NotFound;
        }

        if (await _db.SystemFeedbacks.AnyAsync(
                item => item.UserId == feedback.UserId && !item.IsDeleted,
                ct))
        {
            return FeedbackRestoreOutcome.Conflict;
        }

        feedback.IsDeleted = false;
        feedback.IsVisible = true;
        feedback.ModerationStatus = "Approved";
        feedback.DeletedAt = null;
        feedback.UpdatedAt = restoredAt;
        try
        {
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return FeedbackRestoreOutcome.Restored;
        }
        catch (Exception exception) when (IsSqlServerDeadlock(exception))
        {
            return FeedbackRestoreOutcome.Conflict;
        }
    }

    public async Task AddReplyAsync(FeedbackReply reply, CancellationToken ct = default) =>
        await _db.FeedbackReplies.AddAsync(reply, ct);

    public void RemoveReply(FeedbackReply reply) => _db.FeedbackReplies.Remove(reply);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    private static bool IsSqlServerDeadlock(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 1205 })
            {
                return true;
            }
        }

        return false;
    }
}
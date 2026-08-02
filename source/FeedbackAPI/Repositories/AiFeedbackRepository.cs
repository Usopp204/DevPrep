using System.Data;
using FeedbackAPI.Models;
using FeedbackAPI.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Repositories;

public class AiFeedbackRepository : IAiFeedbackRepository
{
    private readonly FeedbackDbContext _db;

    public AiFeedbackRepository(FeedbackDbContext db)
    {
        _db = db;
    }

    public IQueryable<InterviewFeedback> Query() =>
        _db.InterviewFeedbacks.AsNoTracking().Where(f => !f.IsDeleted);

    public IQueryable<InterviewFeedback> QueryIncludingDeleted() =>
        _db.InterviewFeedbacks.AsNoTracking();

    public IQueryable<InterviewFeedback> PublicQuery() =>
        _db.InterviewFeedbacks
            .AsNoTracking()
            .Where(f => !f.IsDeleted && f.IsVisible);

    public Task<InterviewFeedback?> GetByIdAsync(Guid id, bool tracking = false, CancellationToken ct = default)
    {
        IQueryable<InterviewFeedback> query = _db.InterviewFeedbacks;
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(f => f.InterviewFeedbackId == id, ct);
    }

    public Task<InterviewFeedback?> GetOwnedActiveByIdAsync(
        Guid feedbackId,
        Guid userId,
        bool tracking = false,
        CancellationToken ct = default)
    {
        IQueryable<InterviewFeedback> query = _db.InterviewFeedbacks;
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(
            f => f.InterviewFeedbackId == feedbackId && f.UserId == userId && !f.IsDeleted,
            ct);
    }

    public Task<InterviewFeedback?> GetByUserAndSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default) =>
        _db.InterviewFeedbacks.AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == userId && f.SessionId == sessionId && !f.IsDeleted, ct);

    public Task<bool> ActiveExistsAsync(Guid userId, Guid sessionId, CancellationToken ct = default) =>
        _db.InterviewFeedbacks.AnyAsync(f => f.UserId == userId && f.SessionId == sessionId && !f.IsDeleted, ct);

    public async Task ResolvePendingReportsAsync(Guid interviewFeedbackId, CancellationToken ct = default)
    {
        var reports = await _db.FeedbackReports
            .Where(r => r.InterviewFeedbackId == interviewFeedbackId && r.Status == "Pending")
            .ToListAsync(ct);
        foreach (var r in reports)
        {
            r.Status = "ResolvedByFeedbackDeletion";
            r.ReviewedAt = Helpers.VietnamTime.Now;
        }
    }

    public async Task<bool> TryAddActiveAsync(InterviewFeedback feedback, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await _db.InterviewFeedbacks.AnyAsync(
                existing => existing.UserId == feedback.UserId
                    && existing.SessionId == feedback.SessionId
                    && !existing.IsDeleted,
                ct))
        {
            return false;
        }

        await _db.InterviewFeedbacks.AddAsync(feedback, ct);
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
        CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var feedback = await _db.InterviewFeedbacks
            .FirstOrDefaultAsync(item => item.InterviewFeedbackId == feedbackId && item.IsDeleted, ct);
        if (feedback is null)
        {
            return FeedbackRestoreOutcome.NotFound;
        }

        if (await _db.InterviewFeedbacks.AnyAsync(
                item => item.UserId == feedback.UserId
                    && item.SessionId == feedback.SessionId
                    && !item.IsDeleted,
                ct))
        {
            return FeedbackRestoreOutcome.Conflict;
        }

        feedback.IsDeleted = false;
        feedback.IsVisible = true;
        feedback.ModerationStatus = "Approved";
        feedback.DeletedAt = null;
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
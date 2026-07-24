using FeedbackAPI.Models;
using FeedbackAPI.Repositories.Interfaces;
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
            .Where(f => !f.IsDeleted && f.ModerationStatus == "Approved" && f.IsVisible);

    public Task<SystemFeedback?> GetByIdAsync(Guid id, bool includeReplies = false, bool tracking = false, CancellationToken ct = default)
    {
        IQueryable<SystemFeedback> query = _db.SystemFeedbacks;
        if (includeReplies) query = query.Include(f => f.FeedbackReplies);
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(f => f.FeedbackId == id, ct);
    }

    public Task<int> CountByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.SystemFeedbacks.CountAsync(f => f.UserId == userId, ct);

    public Task<SystemFeedback?> GetLatestByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.SystemFeedbacks
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.FeedbackId)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(SystemFeedback feedback, CancellationToken ct = default) =>
        await _db.SystemFeedbacks.AddAsync(feedback, ct);

    public async Task AddReplyAsync(FeedbackReply reply, CancellationToken ct = default) =>
        await _db.FeedbackReplies.AddAsync(reply, ct);

    public void RemoveReply(FeedbackReply reply) => _db.FeedbackReplies.Remove(reply);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
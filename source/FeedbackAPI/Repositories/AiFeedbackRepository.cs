using FeedbackAPI.Models;
using FeedbackAPI.Repositories.Interfaces;
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
            .Where(f => !f.IsDeleted && f.ModerationStatus == "Approved" && f.IsVisible);

    public Task<InterviewFeedback?> GetByIdAsync(Guid id, bool tracking = false, CancellationToken ct = default)
    {
        IQueryable<InterviewFeedback> query = _db.InterviewFeedbacks;
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(f => f.InterviewFeedbackId == id, ct);
    }

    public Task<InterviewFeedback?> GetByUserAndSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default) =>
        _db.InterviewFeedbacks.AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == userId && f.SessionId == sessionId && !f.IsDeleted, ct);

    public Task<bool> ActiveExistsAsync(Guid userId, Guid sessionId, CancellationToken ct = default) =>
        _db.InterviewFeedbacks.AnyAsync(f => f.UserId == userId && f.SessionId == sessionId && !f.IsDeleted, ct);

    public async Task AddAsync(InterviewFeedback feedback, CancellationToken ct = default) =>
        await _db.InterviewFeedbacks.AddAsync(feedback, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
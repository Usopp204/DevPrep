using FeedbackAPI.Models;
using FeedbackAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Repositories;

public class FeedbackRepository : IFeedbackRepository
{
    private readonly FeedbackDbContext _db;

    public FeedbackRepository(FeedbackDbContext db)
    {
        _db = db;
    }

    // Chỉ lấy bản ghi chưa bị xoá mềm. AsNoTracking cho truy vấn đọc (list/projection).
    public IQueryable<SystemFeedback> QuerySystem() =>
        _db.SystemFeedbacks.AsNoTracking().Where(f => !f.IsDeleted);

    public IQueryable<InterviewFeedback> QueryInterview() =>
        _db.InterviewFeedbacks.AsNoTracking().Where(f => !f.IsDeleted);

    public Task<SystemFeedback?> GetSystemByIdAsync(Guid id, bool includeReplies = false, bool tracking = false)
    {
        IQueryable<SystemFeedback> query = _db.SystemFeedbacks;
        if (includeReplies) query = query.Include(f => f.FeedbackReplies);
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(f => f.FeedbackId == id);
    }

    public Task<InterviewFeedback?> GetInterviewByIdAsync(Guid id, bool tracking = false)
    {
        IQueryable<InterviewFeedback> query = _db.InterviewFeedbacks;
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(f => f.InterviewFeedbackId == id);
    }

    public async Task AddReplyAsync(FeedbackReply reply) => await _db.FeedbackReplies.AddAsync(reply);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}

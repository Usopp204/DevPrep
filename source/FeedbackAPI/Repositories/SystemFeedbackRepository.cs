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

    // Chỉ lấy bản ghi chưa bị xoá mềm. AsNoTracking cho truy vấn đọc (list/projection/search).
    public IQueryable<SystemFeedback> Query() =>
        _db.SystemFeedbacks.AsNoTracking().Where(f => !f.IsDeleted);

    public Task<SystemFeedback?> GetByIdAsync(Guid id, bool includeReplies = false, bool tracking = false)
    {
        IQueryable<SystemFeedback> query = _db.SystemFeedbacks;
        if (includeReplies) query = query.Include(f => f.FeedbackReplies);
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(f => f.FeedbackId == id);
    }

    public async Task AddReplyAsync(FeedbackReply reply) => await _db.FeedbackReplies.AddAsync(reply);

    public void RemoveReply(FeedbackReply reply) => _db.FeedbackReplies.Remove(reply);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}

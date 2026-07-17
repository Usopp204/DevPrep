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

    // Chỉ lấy bản ghi chưa bị xoá mềm. AsNoTracking cho truy vấn đọc (list/projection/search).
    public IQueryable<InterviewFeedback> Query() =>
        _db.InterviewFeedbacks.AsNoTracking().Where(f => !f.IsDeleted);

    // Kèm cả bản ghi đã xoá mềm — cho admin xem/khôi phục.
    public IQueryable<InterviewFeedback> QueryIncludingDeleted() =>
        _db.InterviewFeedbacks.AsNoTracking();

    public Task<InterviewFeedback?> GetByIdAsync(Guid id, bool tracking = false)
    {
        IQueryable<InterviewFeedback> query = _db.InterviewFeedbacks;
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(f => f.InterviewFeedbackId == id);
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}

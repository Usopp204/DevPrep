using InterviewAPI.Models;
using InterviewAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Repositories;

public class InterviewRepository : IInterviewRepository
{
    private readonly InterviewDbContext _db;

    public InterviewRepository(InterviewDbContext db)
    {
        _db = db;
    }

    // Chỉ lấy phiên chưa bị xoá mềm. AsNoTracking cho truy vấn đọc (list/search).
    public IQueryable<InterviewSession> Query() =>
        _db.InterviewSessions.AsNoTracking().Where(s => !s.IsDeleted);

    // Kèm cả bản ghi đã xoá mềm — cho admin xem/khôi phục và đếm tổng phiên.
    public IQueryable<InterviewSession> QueryIncludingDeleted() =>
        _db.InterviewSessions.AsNoTracking();

    public Task<InterviewSession?> GetByIdAsync(Guid id, bool tracking = false, CancellationToken cancellationToken = default)
    {
        IQueryable<InterviewSession> query = _db.InterviewSessions;
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(s => s.SessionId == id, cancellationToken);
    }

    // KHÔNG lọc IsDeleted: admin cần xem lại chi tiết phiên đã xoá trước khi khôi phục
    // (DTO trả về có sẵn cờ IsDeleted/DeletedAt để UI phân biệt).
    public Task<InterviewSession?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.InterviewSessions
            .AsNoTracking()
            .Include(s => s.InterviewRoom)
            .Include(s => s.SessionQuestions)
                .ThenInclude(q => q.ChatMessages)
            .FirstOrDefaultAsync(s => s.SessionId == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}

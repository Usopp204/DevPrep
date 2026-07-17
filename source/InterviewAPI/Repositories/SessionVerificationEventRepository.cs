using InterviewAPI.Models;
using InterviewAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Repositories;

public class SessionVerificationEventRepository : ISessionVerificationEventRepository
{
    private readonly InterviewDbContext _db;

    public SessionVerificationEventRepository(InterviewDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(SessionVerificationEvent evt, CancellationToken ct)
    {
        _db.SessionVerificationEvents.Add(evt);
        await _db.SaveChangesAsync(ct);
    }

    public Task<List<SessionVerificationEvent>> ListBySessionAsync(
        Guid sessionId, string? phase, CancellationToken ct)
    {
        var query = _db.SessionVerificationEvents.AsNoTracking()
            .Where(e => e.SessionId == sessionId);
        if (!string.IsNullOrEmpty(phase))
            query = query.Where(e => e.Phase == phase);
        return query.OrderByDescending(e => e.Id).ToListAsync(ct);
    }
}

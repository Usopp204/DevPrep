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

    public async Task<bool> TryAddIncidentAsync(SessionVerificationEvent evt, CancellationToken ct)
    {
        _db.SessionVerificationEvents.Add(evt);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException) when (evt.IncidentId is not null)
        {
            _db.Entry(evt).State = EntityState.Detached;
            var exists = await _db.SessionVerificationEvents.AsNoTracking()
                .AnyAsync(e => e.IncidentId == evt.IncidentId
                               || (evt.CheckSequence != null
                                   && e.SessionId == evt.SessionId
                                   && e.CheckSequence == evt.CheckSequence), ct);
            if (exists) return false;
            throw;
        }
    }

    public Task<SessionVerificationEvent?> GetIncidentAsync(Guid incidentId, CancellationToken ct) =>
        _db.SessionVerificationEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.IncidentId == incidentId, ct);

    public Task<List<SessionVerificationEvent>> ListBySessionAsync(
        Guid sessionId, string? phase, CancellationToken ct)
    {
        var query = _db.SessionVerificationEvents.AsNoTracking()
            .Where(e => e.SessionId == sessionId);
        if (!string.IsNullOrEmpty(phase))
            query = query.Where(e => e.Phase == phase);
        return query.OrderByDescending(e => e.Id).ToListAsync(ct);
    }

    public Task<SessionVerificationEvent?> GetQuestionCheckAsync(
        Guid sessionId, Guid sessionQuestionId, CancellationToken ct) =>
        _db.SessionVerificationEvents.AsNoTracking().FirstOrDefaultAsync(
            e => e.SessionId == sessionId
                 && e.SessionQuestionId == sessionQuestionId
                 && e.Modality == "Check",
            ct);

    public async Task<bool> TryConsumeQuestionCheckAsync(
        Guid incidentId, Guid sessionId, Guid userId, DateTime consumedAt, CancellationToken ct)
    {
        var affected = await _db.SessionVerificationEvents
            .Where(e => e.IncidentId == incidentId
                        && e.SessionId == sessionId
                        && e.UserId == userId
                        && e.Decision == "Pending"
                        && e.ConsumedAt == null
                        && e.ExpiresAt >= consumedAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.ConsumedAt, consumedAt)
                .SetProperty(e => e.Decision, "Consumed"), ct);
        return affected == 1;
    }

    public Task<List<SessionVerificationEvent>> ListExpiredPendingChecksAsync(
        Guid sessionId, DateTime now, CancellationToken ct) =>
        _db.SessionVerificationEvents.AsNoTracking()
            .Where(e => e.SessionId == sessionId
                        && e.Decision == "Pending"
                        && e.ExpiresAt < now)
            .ToListAsync(ct);

    public async Task<bool> TryMarkCheckMissedAsync(long eventId, CancellationToken ct)
    {
        var affected = await _db.SessionVerificationEvents
            .Where(e => e.Id == eventId && e.Decision == "Pending" && e.ConsumedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Decision, "Flag")
                .SetProperty(e => e.Detail, "{\"Reason\":\"MissedCheck\"}"), ct);
        return affected == 1;
    }
}

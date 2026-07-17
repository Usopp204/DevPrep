using InterviewAPI.Models;
using InterviewAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Repositories;

public class SessionVerificationChallengeRepository : ISessionVerificationChallengeRepository
{
    private readonly InterviewDbContext _db;

    public SessionVerificationChallengeRepository(InterviewDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(SessionVerificationChallenge challenge, CancellationToken ct)
    {
        _db.SessionVerificationChallenges.Add(challenge);
        await _db.SaveChangesAsync(ct);
    }

    public Task<SessionVerificationChallenge?> GetActiveAsync(
        Guid sessionId, string modality, DateTime now, CancellationToken ct) =>
        _db.SessionVerificationChallenges
            .Where(c => c.SessionId == sessionId
                        && c.Modality == modality
                        && c.ConsumedAt == null
                        && c.ExpiresAt > now)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}

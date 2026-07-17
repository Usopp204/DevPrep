using InterviewAPI.Models;

namespace InterviewAPI.Repositories.Interfaces;

public interface ISessionVerificationChallengeRepository
{
    Task AddAsync(SessionVerificationChallenge challenge, CancellationToken ct);

    // Tracked: the newest unconsumed, unexpired challenge for (sessionId, modality), or null.
    Task<SessionVerificationChallenge?> GetActiveAsync(
        Guid sessionId, string modality, DateTime now, CancellationToken ct);

    // Persists changes to an entity previously returned tracked by GetActiveAsync.
    Task SaveChangesAsync(CancellationToken ct);
}

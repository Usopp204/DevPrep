using InterviewAPI.Models;

namespace InterviewAPI.Repositories.Interfaces;

public interface ISessionVerificationEventRepository
{
    Task AddAsync(SessionVerificationEvent evt, CancellationToken ct);

    // Most recent events for a session, newest first — drives entry-gate retry
    // counting and "last in-session check" lookups. Read-only.
    Task<List<SessionVerificationEvent>> ListBySessionAsync(
        Guid sessionId, string? phase, CancellationToken ct);
}

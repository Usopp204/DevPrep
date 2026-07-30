using InterviewAPI.Models;

namespace InterviewAPI.Repositories.Interfaces;

public interface ISessionVerificationEventRepository
{
    Task AddAsync(SessionVerificationEvent evt, CancellationToken ct);

    // Atomically inserts a camera incident. The filtered unique index on IncidentId makes
    // retries and concurrent tabs idempotent.
    Task<bool> TryAddIncidentAsync(SessionVerificationEvent evt, CancellationToken ct);

    Task<SessionVerificationEvent?> GetIncidentAsync(Guid incidentId, CancellationToken ct);

    // Most recent events for a session, newest first — drives entry-gate retry
    // counting and "last in-session check" lookups. Read-only.
    Task<List<SessionVerificationEvent>> ListBySessionAsync(
        Guid sessionId, string? phase, CancellationToken ct);

    Task<SessionVerificationEvent?> GetQuestionCheckAsync(
        Guid sessionId, Guid sessionQuestionId, CancellationToken ct);

    Task<bool> TryConsumeQuestionCheckAsync(
        Guid incidentId, Guid sessionId, Guid userId, DateTime consumedAt, CancellationToken ct);

    Task<List<SessionVerificationEvent>> ListExpiredPendingChecksAsync(
        Guid sessionId, DateTime now, CancellationToken ct);

    Task<bool> TryMarkCheckMissedAsync(long eventId, CancellationToken ct);
}

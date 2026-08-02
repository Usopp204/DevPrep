using InterviewAPI.DTOs.Session;
using InterviewAPI.Models;

namespace InterviewAPI.Repositories.Interfaces;

public interface ISessionRepository
{
    Task<InterviewSession?> GetByIdAsync(Guid id, CancellationToken ct);

    // Newest InProgress/Paused session for a user (Dashboard "resume your interview" banner).
    // Tracking (not AsNoTracking) — the caller may finalize an expired session in place via
    // FinalizeIfTimeExpiredAsync, same reasoning as GetByIdAsync.
    Task<InterviewSession?> GetActiveSessionAsync(Guid userId, CancellationToken ct);
    Task<InterviewSession?> GetWithQuestionsAsync(Guid sessionId, CancellationToken ct);
    Task<InterviewSession?> GetWithFullHistoryAsync(Guid sessionId, CancellationToken ct);
    Task<InterviewSession> CreateAsync(InterviewSession session, CancellationToken ct);
    Task AddSessionQuestionsAsync(List<SessionQuestion> questions, CancellationToken ct);
    Task<InterviewRoom> CreateRoomAsync(InterviewRoom room, CancellationToken ct);
    Task<ChatMessage> AddChatMessageAsync(ChatMessage message, CancellationToken ct);
    Task<SessionQuestion?> GetQuestionByIdAsync(Guid sessionQuestionId, CancellationToken ct);
    Task<List<ChatMessage>> GetMessagesByQuestionAsync(Guid sessionQuestionId, CancellationToken ct);
    Task<SessionQuestion?> GetNextUnansweredQuestionAsync(Guid sessionId, int afterOrderIndex, CancellationToken ct);

    // True once this question already has a User/Answer ChatMessage — the same "answered" rule
    // GetNextUnansweredQuestionAsync uses. Checked BEFORE the IncrementAnsweredIfAsync claim below
    // so a duplicate submission for an already-answered question is rejected before it can consume
    // a fresh counter value (see SendMessageAsync's Step B.1 comment for why the counter claim
    // alone isn't enough for duplicates that arrive sequentially rather than simultaneously).
    Task<bool> HasUserAnswerAsync(Guid sessionQuestionId, CancellationToken ct);
    Task<bool> HasSubstantiveAnswerAsync(Guid sessionId, CancellationToken ct);
    Task<List<InterviewSession>> GetByUserAsync(Guid userId, CancellationToken ct);
    Task<(IReadOnlyList<InterviewSession> Items, int TotalItems, IReadOnlyList<string> PositionOptions)> GetHistoryAsync(
        Guid userId,
        SessionHistoryQuery query,
        CancellationToken ct);
    Task<IReadOnlyList<string>> GetHistoryQuestionTypeOptionsAsync(Guid userId, CancellationToken ct);
    Task<InterviewSession?> GetChatLogAsync(Guid userId, Guid sessionId, CancellationToken ct);
    Task<int> CountSystemFeedbackQualifyingSessionsAsync(Guid userId, CancellationToken ct);
    Task<InterviewSession?> GetFeedbackEligibilitySessionAsync(Guid userId, Guid sessionId, CancellationToken ct);
    Task<InterviewRoom?> GetRoomBySessionAsync(Guid sessionId, CancellationToken ct);
    Task UpdateRoomAsync(InterviewRoom room, CancellationToken ct);
    Task<bool> UpdateStatusIfAsync(Guid userId, Guid sessionId, string currentStatus, string newStatus, CancellationToken ct);

    // Atomic per-turn idempotency guard for SendMessageAsync: claims a turn by incrementing
    // AnsweredQuestions only if it still equals expectedCurrentCount. A duplicate/racing call
    // (double-fire click, retry) reads a stale expected value and loses — same conditional-
    // UPDATE pattern as UpdateStatusIfAsync above.
    Task<bool> IncrementAnsweredIfAsync(Guid sessionId, int expectedCurrentCount, CancellationToken ct);
    Task<bool> AcceptIntegrityPolicyIfAsync(Guid sessionId, CancellationToken ct);

    // Atomic one-time claim for a question's hint quota — same conditional-UPDATE pattern as
    // AcceptIntegrityPolicyIfAsync. Returns false if a hint was already used for this question.
    Task<bool> TryConsumeHintAsync(Guid sessionQuestionId, Guid sessionId, CancellationToken ct);

    // Refunds the hint quota (HintUsedAt back to null) when the ai-service call after a
    // successful claim fails — called by SessionService.HintAsync, never on its own.
    Task ReleaseHintAsync(Guid sessionQuestionId, CancellationToken ct);

    // Atomic per-session Skip cooldown claim — same conditional-UPDATE pattern as
    // AcceptIntegrityPolicyIfAsync. Allowed (and LastSkipAtUtc stamped) only if no prior skip is
    // recorded or cooldownSeconds has fully elapsed since the last one. When blocked, returns how
    // many seconds remain so the caller can report Retry-After.
    Task<(bool Allowed, int RetryAfterSeconds)> TryConsumeSkipCooldownAsync(
        Guid sessionId, int cooldownSeconds, CancellationToken ct);

    // Rollback for IncrementAnsweredIfAsync when a step AFTER the successful claim fails
    // (e.g. the ChatMessage insert throws) — safe as an unconditional decrement because only
    // the request that just won the claim calls this, before returning to any other caller.
    Task DecrementAnsweredAsync(Guid sessionId, CancellationToken ct);

    // Flag-only audit counter (mandatory-camera policy) — see SessionService.ReportCameraViolationAsync.
    Task<int> IncrementCameraViolationAsync(Guid sessionId, CancellationToken ct);

    // Atomic auto-resume for a pause whose quota has expired (SessionService.
    // AutoResumeIfPauseExpiredAsync): only applies if the session is still exactly as read
    // (Status=="Paused" AND PausedAt==expectedPausedAt) — same conditional-UPDATE pattern as
    // UpdateStatusIfAsync/IncrementAnsweredIfAsync. Guards against two concurrent callers (e.g.
    // overlapping GetTimerAsync polls) both computing and double-applying the paused-seconds
    // delta: only the first to hit this WHERE clause wins, the second sees PausedAt already
    // changed and no-ops.
    Task<bool> TryAutoResumeExpiredPauseAsync(
        Guid sessionId, DateTime expectedPausedAt, int newTotalPausedSeconds, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

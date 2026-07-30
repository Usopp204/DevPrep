using InterviewAPI.DTOs;
using InterviewAPI.DTOs.Session;
using InterviewAPI.DTOs.Biometrics;

namespace InterviewAPI.Services.Interfaces;

public interface ISessionService
{
    Task<ConfigResponse> ConfigDurationAsync(Guid userId, ConfigDurationRequest req, CancellationToken ct);
    Task<ConfigResponse> ConfigLevelAsync(Guid userId, ConfigLevelRequest req, CancellationToken ct);
    Task<ConfigResponse> ConfigInteractionAsync(Guid userId, ConfigInteractionRequest req, CancellationToken ct);
    Task<InitResponse> InitAsync(Guid userId, InitRequest req, CancellationToken ct);
    Task<GenerateQuestionResponse> GenerateQuestionAsync(Guid userId, Guid sessionId, CancellationToken ct);
    Task<MessageResponse> SendMessageAsync(Guid userId, Guid sessionId, SendMessageRequest req, CancellationToken ct);
    Task<TimerResponse>   GetTimerAsync(Guid userId, Guid sessionId, CancellationToken ct);
    Task<InSessionCheckDueResponse> GetInSessionCheckAsync(
        Guid userId, Guid sessionId, Guid sessionQuestionId, CancellationToken ct);
    Task                  TrackTimeAsync(Guid userId, Guid sessionId, TrackTimeRequest req, CancellationToken ct);
    Task<EndResponse>     CompleteSessionAsync(Guid userId, Guid sessionId, CancellationToken ct);
    Task<EndResponse>     EndSessionAsync(Guid userId, Guid sessionId, CancellationToken ct);
    Task<ResumeResponse>  ResumeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct);
    Task<PauseResponse>   PauseSessionAsync(Guid userId, Guid sessionId, CancellationToken ct);
    Task<UnpauseResponse> UnpauseSessionAsync(Guid userId, Guid sessionId, CancellationToken ct);

    // Records one idempotent camera incident after the reconnect grace period. The incident is
    // a direct cheating strike and may end a Strict session when the shared cap is reached.
    Task<ReportCameraViolationResponse> ReportCameraViolationAsync(
        Guid userId, Guid sessionId, ReportCameraViolationRequest req, CancellationToken ct);


    // Room UX Phase 3 — marks the onboarding tour as seen for this user (once ever, not per session).
    Task MarkRoomTourSeenAsync(Guid userId, CancellationToken ct);

    Task                  SoftDeleteAsync(Guid userId, Guid sessionId, CancellationToken ct);
    Task<List<SessionListItemDto>> GetMySessionsAsync(Guid userId, CancellationToken ct);
    Task<SessionHistoryResult> GetSessionHistoryAsync(
        Guid userId,
        SessionHistoryQuery query,
        CancellationToken ct);
    Task<SessionChatLogResponse> GetSessionChatLogAsync(Guid userId, Guid sessionId, CancellationToken ct);
    Task<FeedbackEligibilityResponse> GetFeedbackEligibilityAsync(Guid userId, CancellationToken ct);
    Task<SessionFeedbackEligibilityResponse> GetSessionFeedbackEligibilityAsync(Guid userId, Guid sessionId, CancellationToken ct);
    Task<TranscriptResponse>       GetTranscriptAsync(Guid userId, Guid sessionId, CancellationToken ct);

    // True if the session exists (not soft-deleted) and belongs to the user. Used by EvaluationAPI
    // to verify ownership (the Evaluation tables have no UserId of their own).
    Task<bool> OwnsSessionAsync(Guid userId, Guid sessionId, CancellationToken ct);

    Task<IntegrityPolicyStatusResponse> GetIntegrityPolicyStatusAsync(Guid userId, Guid sessionId, CancellationToken ct);
    Task<IntegrityPolicyStatusResponse> AcceptIntegrityPolicyAsync(Guid userId, Guid sessionId, CancellationToken ct);
}

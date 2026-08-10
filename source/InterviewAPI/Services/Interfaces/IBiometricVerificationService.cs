using InterviewAPI.DTOs.Biometrics;
using InterviewAPI.DTOs.Session;

namespace InterviewAPI.Services.Interfaces;

public interface IBiometricVerificationService
{
    // modality: "Face" | "Voice". Generates a server-side challenge for the session,
    // stores the expected value with a short TTL, and returns only the instruction.
    Task<VerificationChallengeResponse> IssueChallengeAsync(
        Guid userId, Guid sessionId, string modality, CancellationToken ct);

    // phase: "Entry" | "InSession" — recorded on the SessionVerificationEvents audit row
    // (Milestone 4 groundwork). Defaults to "Entry" for existing callers.
    Task<VerificationResultResponse> VerifyFaceAsync(
        Guid userId, Guid sessionId, List<IFormFile> frames, CancellationToken ct, string phase = "Entry");

    Task<VerificationResultResponse> VerifyVoiceAsync(
        Guid userId, Guid sessionId, IFormFile file, CancellationToken ct, string phase = "Entry");

    // Read-only decision: is this user allowed past the room entry gate right now?
    // Does not itself verify anything — it inspects enrollment + recent Entry-phase
    // events written by VerifyFaceAsync/VerifyVoiceAsync. Milestone 4 groundwork —
    // not called from any Room UI yet.
    Task<EntryGateStatusResponse> GetEntryGateStatusAsync(Guid userId, Guid sessionId, CancellationToken ct);

    // One in-session cheating check: single-frame face match + multi-face detection. Called by
    // the Room once per new question (rate-limited server-side). See the implementation for the
    // strike/Strictness rules.
    Task<InSessionVerifyResponse> VerifyInSessionAsync(
        Guid userId, Guid sessionId, Guid checkId, IFormFile frame, CancellationToken ct);

    // Transcribes one microphone answer and performs speaker-only verification against the
    // enrolled voice template. No challenge phrase or voice-liveness rule is applied.
    Task<VoiceAnswerVerifyResponse> VerifyVoiceAnswerAsync(
        Guid userId, Guid sessionId, Guid checkId, IFormFile audio, CancellationToken ct);

    // Records one client-reported tab-switch / focus-loss / paste event as an audit row
    // (Modality="Tab"). Phase 1 is record-only — these rows carry no strike weight. Expected
    // failure cases come back as TabEventResponse.Outcome instead of exceptions so the controller
    // can map them to distinct HTTP statuses; see the implementation.
    Task<TabEventResponse> RecordTabEventAsync(
        Guid userId, Guid sessionId, RecordTabEventRequest req, CancellationToken ct);
}

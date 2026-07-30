using System.Text.Json;
using InterviewAPI.Clients;
using InterviewAPI.DTOs.Biometrics;
using InterviewAPI.Helpers;
using InterviewAPI.Models;
using InterviewAPI.Repositories.Interfaces;
using InterviewAPI.Services.Interfaces;

namespace InterviewAPI.Services;

public class BiometricVerificationService : IBiometricVerificationService
{
    // Curated Vietnamese challenge phrases (matches the Deepgram Vietnamese STT).
    // Random pick per challenge; a replayed recording won't match a fresh phrase.
    private static readonly string[] ChallengePhrases =
    {
        "Hôm nay tôi tự tin bước vào buổi phỏng vấn quan trọng này.",
        "Tôi cam kết tự mình hoàn thành toàn bộ bài phỏng vấn.",
        "Lập trình là hành trình học hỏi không ngừng mỗi ngày.",
        "Buổi sáng mùa thu, bầu trời trong xanh và gió mát.",
        "Kiên trì luyện tập giúp tôi tiến bộ từng chút một.",
        "Công nghệ thay đổi nhanh, người học cần thích nghi nhanh hơn.",
        "Tôi thích giải quyết vấn đề bằng tư duy logic rõ ràng.",
        "Chuẩn bị kỹ lưỡng là chìa khóa của mọi thành công.",
        "Mỗi câu hỏi khó là một cơ hội để thể hiện bản thân.",
        "Đội nhóm mạnh được xây từ sự tin tưởng và tôn trọng.",
    };

    private readonly ISessionVerificationChallengeRepository _challengeRepository;
    private readonly ISessionVerificationEventRepository _eventRepository;
    private readonly IProfileBiometricsRepository _biometricsRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IConfiguration _configuration;

    public BiometricVerificationService(
        ISessionVerificationChallengeRepository challengeRepository,
        ISessionVerificationEventRepository eventRepository,
        IProfileBiometricsRepository biometricsRepository,
        ISessionRepository sessionRepository,
        IAiServiceClient aiServiceClient,
        IConfiguration configuration)
    {
        _challengeRepository = challengeRepository;
        _eventRepository = eventRepository;
        _biometricsRepository = biometricsRepository;
        _sessionRepository = sessionRepository;
        _aiServiceClient = aiServiceClient;
        _configuration = configuration;
    }

    public async Task<VerificationChallengeResponse> IssueChallengeAsync(
        Guid userId, Guid sessionId, string modality, CancellationToken ct)
    {
        EnsureEnabled();
        await EnsureSessionOwnedAsync(userId, sessionId, ct);
        await EnsureEnrolledAsync(userId, modality, ct);

        var ttlSeconds = _configuration.GetValue("Biometrics:ChallengeTtlSeconds", 120);
        string challengeJson;
        string? direction = null;
        string? phrase = null;

        if (modality == ProfileBiometricsService.ModalityFace)
        {
            direction = Random.Shared.Next(2) == 0 ? "left" : "right";
            challengeJson = JsonSerializer.Serialize(new { type = "turn_head", direction });
        }
        else
        {
            phrase = ChallengePhrases[Random.Shared.Next(ChallengePhrases.Length)];
            challengeJson = JsonSerializer.Serialize(new { phrase });
        }

        await _challengeRepository.AddAsync(new SessionVerificationChallenge
        {
            SessionId = sessionId,
            UserId = userId,
            Modality = modality,
            ChallengeJson = challengeJson,
            ExpiresAt = VietnamTime.Now.AddSeconds(ttlSeconds),
            CreatedAt = VietnamTime.Now,
        }, ct);

        return new VerificationChallengeResponse
        {
            Modality = modality,
            Direction = direction,
            Phrase = phrase,
            ExpiresInSeconds = ttlSeconds,
        };
    }

    public async Task<VerificationResultResponse> VerifyFaceAsync(
        Guid userId, Guid sessionId, List<IFormFile> frames, CancellationToken ct, string phase = "Entry")
    {
        EnsureEnabled();
        if (frames is null || frames.Count == 0)
            throw new ArgumentException("At least one frame is required.");
        await EnsureSessionOwnedAsync(userId, sessionId, ct);

        // Entry retry cap (per session): once EntryMaxRetries face attempts have failed for
        // this session, reject before consuming a challenge or calling ai-service. The user
        // must create a new session, which starts with a fresh budget.
        if (phase == "Entry")
        {
            var maxRetries = _configuration.GetValue("Biometrics:EntryMaxRetries", 3);
            var entryEvents = await _eventRepository.ListBySessionAsync(sessionId, "Entry", ct);
            var failedAttempts = entryEvents.Count(
                e => e.Modality == ProfileBiometricsService.ModalityFace && e.Decision != "Pass");
            if (failedAttempts >= maxRetries)
                throw new InvalidOperationException(
                    "Maximum verification attempts reached for this session. Please create a new session.");
        }

        var challenge = await ConsumeChallengeAsync(
            sessionId, ProfileBiometricsService.ModalityFace, ct);
        var embedding = await GetStoredEmbeddingAsync(
            userId, ProfileBiometricsService.ModalityFace, ct);

        var result = await _aiServiceClient.VerifyFaceLiveAsync(
            frames, embedding, challenge.ChallengeJson, ct);

        var response = new VerificationResultResponse
        {
            Modality = ProfileBiometricsService.ModalityFace,
            Passed = result.Passed,
            Score = result.IdentityScore,
            Live = result.Live,
            Reason = result.Passed
                ? null
                : result.Reason ?? (result.Live ? "Face does not match the enrolled template." : null),
        };

        await RecordEventAsync(sessionId, userId, phase, ProfileBiometricsService.ModalityFace,
            score: result.IdentityScore, livenessPassed: result.Live, matched: result.Passed,
            decision: result.Passed ? "Pass" : "Fail",
            detail: new { result.Reason, result.YawRange, result.MovedEnough, result.DirectionOk }, ct);

        return response;
    }

    public async Task<VerificationResultResponse> VerifyVoiceAsync(
        Guid userId, Guid sessionId, IFormFile file, CancellationToken ct, string phase = "Entry")
    {
        EnsureEnabled();
        if (file is null || file.Length == 0)
            throw new ArgumentException("An audio sample is required.");
        await EnsureSessionOwnedAsync(userId, sessionId, ct);

        var challenge = await ConsumeChallengeAsync(
            sessionId, ProfileBiometricsService.ModalityVoice, ct);
        var embedding = await GetStoredEmbeddingAsync(
            userId, ProfileBiometricsService.ModalityVoice, ct);

        var expectedPhrase = JsonDocument.Parse(challenge.ChallengeJson)
            .RootElement.GetProperty("phrase").GetString() ?? string.Empty;

        var result = await _aiServiceClient.VerifyVoiceChallengeAsync(
            file, embedding, expectedPhrase, ct);

        string? reason = null;
        if (!result.Passed)
        {
            reason = result.ContentOk
                ? "Voice does not match the enrolled template."
                : "The spoken phrase does not match the challenge.";
        }

        await RecordEventAsync(sessionId, userId, phase, ProfileBiometricsService.ModalityVoice,
            score: result.SpeakerScore, livenessPassed: null, matched: result.Passed,
            decision: result.Passed ? "Pass" : "Fail",
            detail: new { Reason = reason, result.ContentOk, result.ContentScore, result.Transcript }, ct);

        return new VerificationResultResponse
        {
            Modality = ProfileBiometricsService.ModalityVoice,
            Passed = result.Passed,
            Score = result.SpeakerScore,
            ContentOk = result.ContentOk,
            Transcript = result.Transcript,
            Reason = reason,
        };
    }

    // Face-only entry gate: the room opens once THIS session has one passed Entry/Face
    // liveness check. Voice stays out of the entry decision (the fusion helper remains
    // dormant for the future in-session strictness work). Failed attempts are counted
    // per session, so a fresh session always starts with the full retry budget.
    public async Task<EntryGateStatusResponse> GetEntryGateStatusAsync(
        Guid userId, Guid sessionId, CancellationToken ct)
    {
        var maxRetries = _configuration.GetValue("Biometrics:EntryMaxRetries", 3);

        if (!_configuration.GetValue("Biometrics:Enabled", false))
            return new EntryGateStatusResponse
            {
                FaceEnrolled = true,
                Allowed = true,
                AttemptsRemaining = maxRetries,
                Reason = "Biometric verification is disabled.",
            };

        await EnsureSessionOwnedAsync(userId, sessionId, ct);

        var faceRow = await _biometricsRepository.GetAsync(userId, ProfileBiometricsService.ModalityFace, ct);
        if (faceRow is null)
        {
            return new EntryGateStatusResponse
            {
                FaceEnrolled = false,
                Allowed = false,
                AttemptsRemaining = maxRetries,
                Reason = "Please enroll your face on your profile before entering the room.",
            };
        }

        var events = await _eventRepository.ListBySessionAsync(sessionId, "Entry", ct);
        var faceEvents = events.Where(e => e.Modality == ProfileBiometricsService.ModalityFace).ToList();
        var facePassed = faceEvents.Any(e => e.Decision == "Pass");
        var failedAttempts = faceEvents.Count(e => e.Decision != "Pass");
        var attemptsRemaining = Math.Max(0, maxRetries - failedAttempts);

        if (facePassed)
        {
            return new EntryGateStatusResponse
            {
                FaceEnrolled = true,
                Allowed = true,
                AttemptsUsed = failedAttempts,
                AttemptsRemaining = attemptsRemaining,
                Reason = "Entry verification passed.",
            };
        }

        return new EntryGateStatusResponse
        {
            FaceEnrolled = true,
            Allowed = false,
            AttemptsUsed = failedAttempts,
            AttemptsRemaining = attemptsRemaining,
            Reason = attemptsRemaining == 0
                ? "Maximum verification attempts reached for this session. Please create a new session."
                : "Face verification required before entering the room.",
        };
    }

    // In-session cheating check: single-frame face match against the enrolled template, no
    // challenge/liveness (see AiServiceClient.VerifyFaceAsync — POST /internal/biometrics/verify).
    // Three strike-worthy outcomes: identity mismatch, multiple faces in frame, and repeated "no
    // face detected" (looking away/off-frame too often). A single no-face check is NOT a strike
    // on its own — too easy to mistake for innocently looking down at notes — but every
    // NoFaceStrikeThreshold of them accumulated (not necessarily consecutive) converts into one
    // strike, same weight as a Flag. Biometrics:InSessionStrictness controls what happens once
    // the strike cap is hit: "Flag" only ever records/reports, "Strict" ends the session on the
    // strike that reaches the cap.
    //
    // The Room calls this once per NEW QUESTION, so the rate-limit below is what actually
    // protects ai-service: skipping questions rapidly would otherwise fire a burst of calls,
    // and each one occupies ai-service for ~130-350ms (single uvicorn worker, no thread pool —
    // a burst stalls every other ai-service consumer, not just this check).
    // TODO: ai-service runs --workers 1 with synchronous handlers; consider a thread pool or
    // more workers before raising the check frequency any further.
    public async Task<InSessionVerifyResponse> VerifyInSessionAsync(
        Guid userId, Guid sessionId, IFormFile frame, CancellationToken ct)
    {
        EnsureEnabled();
        if (frame is null || frame.Length == 0)
            throw new ArgumentException("A frame is required.");

        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new KeyNotFoundException("Session not found.");
        if (session.Status != "InProgress")
            throw new InvalidOperationException("Session is not in progress.");

        var maxStrikes = _configuration.GetValue("Biometrics:InSessionMaxStrikes", 3);
        var strictness = _configuration.GetValue("Biometrics:InSessionStrictness", "Flag");
        var minIntervalSeconds = _configuration.GetValue("Biometrics:InSessionMinIntervalSeconds", 25);
        var noFaceStrikeThreshold = _configuration.GetValue("Biometrics:NoFaceStrikeThreshold", 3);

        // Rate limit (server-side on purpose — the client mirrors nothing here, this IS the
        // guard): too soon after the previous check, no-op. Returns a neutral "passed" result
        // rather than an error so a rapid-skip burst is silently absorbed instead of surfacing
        // scary warnings; nothing is recorded and no strike is counted for a skipped check.
        var priorEvents = await _eventRepository.ListBySessionAsync(sessionId, "InSession", ct);
        var lastCheckAt = priorEvents.FirstOrDefault()?.CreatedAt;
        if (lastCheckAt is not null
            && (VietnamTime.Now - lastCheckAt.Value).TotalSeconds < minIntervalSeconds)
        {
            var priorEffectiveStrikes = ComputeEffectiveStrikeCount(priorEvents, noFaceStrikeThreshold);
            return new InSessionVerifyResponse
            {
                Passed = true,
                NoFaceDetected = false,
                WarningType = "None",
                StrikeCount = Math.Min(priorEffectiveStrikes, maxStrikes),
                MaxStrikes = maxStrikes,
                NoFaceCount = 0,
                NoFaceStrikeThreshold = noFaceStrikeThreshold,
                ShouldEndSession = false,
                Message = "Check skipped (too soon after the previous one).",
            };
        }

        var embedding = await GetStoredEmbeddingAsync(userId, ProfileBiometricsService.ModalityFace, ct);

        bool isStrike;
        bool noFaceDetected = false;
        string decision;
        string warningType;
        object detail;
        string message;

        try
        {
            var result = await _aiServiceClient.VerifyFaceAsync(frame, embedding, ct);
            isStrike = !result.Match;
            decision = result.Match ? "Pass" : "Flag";
            warningType = result.Match ? "None" : "IdentityMismatch";
            detail = new { result.Score, result.Threshold, Reason = result.Match ? null : "Identity mismatch" };
            message = result.Match
                ? "Identity confirmed."
                : "The face in frame does not match your enrolled profile.";
        }
        catch (BiometricQualityException ex) when (ex.FaceCount is int faceCount)
        {
            isStrike = true;
            decision = "Flag";
            warningType = "MultiFace";
            detail = new { Reason = "Multiple faces detected", FaceCount = faceCount };
            message = $"Another person appears to be in frame with you ({faceCount} faces detected). " +
                      "If this continues, it will be treated as a possible attempt to get help during the interview.";
        }
        catch (BiometricQualityException)
        {
            // No face / blurry / too small / ... — not a strike by itself, see the method
            // comment above; NoFaceCount below tracks accumulation toward a converted strike.
            isStrike = false;
            noFaceDetected = true;
            decision = "NoFace";
            warningType = "NoFace";
            detail = new { Reason = "No face detected" };
            message = "We couldn't see your face — please make sure you're in frame.";
        }

        await RecordEventAsync(sessionId, userId, "InSession", ProfileBiometricsService.ModalityFace,
            score: null, livenessPassed: null, matched: !isStrike && !noFaceDetected,
            decision: decision, detail: detail, ct);

        var events = await _eventRepository.ListBySessionAsync(sessionId, "InSession", ct);
        var noFaceCount = events.Count(e => e.Modality == ProfileBiometricsService.ModalityFace && e.Decision == "NoFace");
        var effectiveStrikeCount = ComputeEffectiveStrikeCount(events, noFaceStrikeThreshold);

        // This NoFace event just completed a fresh group of noFaceStrikeThreshold — it converts
        // into a strike right now, with the same weight as a Flag.
        var noFaceJustBecameStrike = noFaceDetected && noFaceCount % noFaceStrikeThreshold == 0;
        var strikeJustHappened = isStrike || noFaceJustBecameStrike;

        var shouldEndSession = strikeJustHappened
            && string.Equals(strictness, "Strict", StringComparison.OrdinalIgnoreCase)
            && effectiveStrikeCount >= maxStrikes;

        // Always clamp what's shown — the real effectiveStrikeCount keeps growing past maxStrikes
        // under Strictness="Flag" (nothing ever ends the session to stop it), but the UI must
        // never display more than the cap (see InSessionVerifyResponse.StrikeCount).
        var displayStrikeCount = Math.Min(effectiveStrikeCount, maxStrikes);
        var noFaceGroupProgress = noFaceCount % noFaceStrikeThreshold;

        if (shouldEndSession)
        {
            await EndSessionForCheatingAsync(sessionId, ct);
            // Baked-in count on purpose: this exact message is also shown standalone as the
            // final chat bubble (see interview-room.js), which does not go through the client's
            // generic "(X/Y)" strike-count suffix the way the other strike messages below do.
            message = $"Maximum cheating warnings reached ({displayStrikeCount}/{maxStrikes}). This interview has been ended.";
        }
        else if (noFaceJustBecameStrike)
        {
            message = "Repeated no-face detections counted as a cheating warning.";
        }
        else if (noFaceDetected)
        {
            message = $"{message} You've stepped out of frame {noFaceGroupProgress}/{noFaceStrikeThreshold} times.";
        }
        // IdentityMismatch/MultiFace/the noFaceJustBecameStrike message above stay strike-count
        // free — no more generic " Warning X/Y" suffix baked in here; the client attaches
        // "(StrikeCount/MaxStrikes)" once, consistently, for every one of these three cases.

        return new InSessionVerifyResponse
        {
            Passed = !isStrike && !noFaceDetected,
            NoFaceDetected = noFaceDetected,
            WarningType = warningType,
            StrikeCount = displayStrikeCount,
            MaxStrikes = maxStrikes,
            NoFaceCount = noFaceGroupProgress,
            NoFaceStrikeThreshold = noFaceStrikeThreshold,
            NoFaceJustBecameStrike = noFaceJustBecameStrike,
            ShouldEndSession = shouldEndSession,
            Message = message,
        };
    }

    // Shared by the main check and the rate-limit-skip branch so both report the same number.
    // Real (unclamped) count — callers clamp to maxStrikes only for display.
    private static int ComputeEffectiveStrikeCount(IEnumerable<SessionVerificationEvent> events, int noFaceStrikeThreshold)
    {
        var flagCount = events.Count(e => e.Modality == ProfileBiometricsService.ModalityFace && e.Decision == "Flag");
        var noFaceCount = events.Count(e => e.Modality == ProfileBiometricsService.ModalityFace && e.Decision == "NoFace");
        return flagCount + noFaceCount / noFaceStrikeThreshold;
    }

    // Ends the session for exceeding the in-session cheating strike cap (Strictness="Strict").
    // Status="Cancelled" — distinct from the normal Completed/Expired outcomes EndSessionAsync
    // produces, so history/reports can tell a cheating-ended session apart. Deliberately does
    // NOT touch credit (no refund call) — SessionService.EndSessionAsync doesn't refund on a
    // normal end either, so simply never calling refund here is enough; awarding a refund here
    // specifically would let someone deliberately trigger this to end a session for free.
    private async Task EndSessionForCheatingAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session is null) return;

        session.Status = "Cancelled";
        session.EndedAt = VietnamTime.Now;
        session.UpdatedAt = VietnamTime.Now;

        var room = await _sessionRepository.GetRoomBySessionAsync(sessionId, ct);
        if (room is not null)
        {
            room.Status = "Closed";
            room.ClosedAt = VietnamTime.Now;
            await _sessionRepository.UpdateRoomAsync(room, ct);
        }
        else
        {
            await _sessionRepository.SaveChangesAsync(ct);
        }
    }

    private async Task RecordEventAsync(
        Guid sessionId, Guid userId, string phase, string modality,
        double? score, bool? livenessPassed, bool matched, string decision, object detail, CancellationToken ct)
    {
        await _eventRepository.AddAsync(new SessionVerificationEvent
        {
            SessionId = sessionId,
            UserId = userId,
            Phase = phase,
            Modality = modality,
            Score = score,
            LivenessPassed = livenessPassed,
            Matched = matched,
            Decision = decision,
            Detail = JsonSerializer.Serialize(detail),
            CreatedAt = VietnamTime.Now,
        }, ct);
    }

    private void EnsureEnabled()
    {
        if (!_configuration.GetValue("Biometrics:Enabled", false))
            throw new InvalidOperationException("Biometric verification is disabled.");
    }

    private async Task EnsureSessionOwnedAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new KeyNotFoundException("Session not found.");
    }

    private async Task EnsureEnrolledAsync(Guid userId, string modality, CancellationToken ct)
    {
        var row = await _biometricsRepository.GetAsync(userId, modality, ct);
        if (row is null)
            throw new InvalidOperationException(
                $"No {modality.ToLowerInvariant()} enrollment found. Please enroll on your profile first.");
    }

    // Loads the newest active challenge and marks it consumed (single use, anti-replay).
    private async Task<SessionVerificationChallenge> ConsumeChallengeAsync(
        Guid sessionId, string modality, CancellationToken ct)
    {
        var challenge = await _challengeRepository.GetActiveAsync(
            sessionId, modality, VietnamTime.Now, ct);
        if (challenge is null)
            throw new InvalidOperationException(
                "No active challenge for this session. Request a new challenge and try again.");
        challenge.ConsumedAt = VietnamTime.Now;
        await _challengeRepository.SaveChangesAsync(ct);
        return challenge;
    }

    private async Task<float[]> GetStoredEmbeddingAsync(Guid userId, string modality, CancellationToken ct)
    {
        var row = await _biometricsRepository.GetAsync(userId, modality, ct);
        if (row is null)
            throw new InvalidOperationException(
                $"No {modality.ToLowerInvariant()} enrollment found. Please enroll on your profile first.");
        return EmbeddingCodec.ToFloats(row.Embedding);
    }
}

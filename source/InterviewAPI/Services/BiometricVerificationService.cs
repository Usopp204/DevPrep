using System.Text.Json;
using InterviewAPI.Clients;
using InterviewAPI.DTOs.AiService;
using InterviewAPI.DTOs.Biometrics;
using InterviewAPI.DTOs.Session;
using InterviewAPI.Helpers;
using InterviewAPI.Models;
using InterviewAPI.Repositories.Interfaces;
using InterviewAPI.Services.Interfaces;

namespace InterviewAPI.Services;

public class BiometricVerificationService : IBiometricVerificationService
{
    // Audit modality for tab-switch / window-blur events. Intentionally absent from
    // CheatingStrikeHelper's DIRECT-strike whitelist — a single tab switch is not, on its own,
    // treated as evidence of cheating the way a Paste or a face mismatch is. It still accumulates
    // the same way NoFace does though: every Biometrics:TabStrikeThreshold of them converts into
    // one effective strike (see BuildTabStrikeResultAsync), so it is not a permanent free pass.
    public const string ModalityTab = "Tab";

    // Audit modality for paste-into-answer events. Unlike ModalityTab, this IS in
    // CheatingStrikeHelper's direct-strike whitelist (Phase 3): a paste is direct evidence of
    // copied content, so every single one is its own full strike, sharing the same pool as
    // Face/Voice/Camera/Check mismatches.
    public const string ModalityPaste = "Paste";

    // Accepted RecordTabEventRequest.EventType values (case-sensitive, like every other
    // plain-string domain value in this service).
    public static readonly string[] TabEventTypes = { "TabHidden", "WindowBlur", "Paste" };

    // Subset of TabEventTypes that carries a real "time away" measurement and is therefore
    // subject to Biometrics:TabSwitchMinAwayMs. "Paste" is deliberately excluded: it is an
    // instantaneous event, so its DurationMs is always 0 and must not be read as "too short".
    public static readonly string[] DurationBearingTabEventTypes = { "TabHidden", "WindowBlur" };

    // RecordTabEventAsync outcomes — the controller maps each to its HTTP status.
    public const string TabOutcomeRecorded = "Recorded";
    public const string TabOutcomeDuplicate = "Duplicate";
    public const string TabOutcomeBelowThreshold = "BelowThreshold";
    public const string TabOutcomeDisabled = "Disabled";
    public const string TabOutcomeNotOwner = "NotOwner";
    public const string TabOutcomeNotInProgress = "NotInProgress";
    public const string TabOutcomeInvalidEventType = "InvalidEventType";
    public const string TabOutcomeInvalidIncidentId = "InvalidIncidentId";

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
    // The Room calls this once per NEW QUESTION. What actually protects ai-service is the
    // one-time-use check token: SessionService.GetInSessionCheckAsync issues at most one pending
    // checkId per SessionQuestion and TryConsumeQuestionCheckAsync below consumes it, so skipping
    // questions rapidly cannot fire a burst of calls. That matters because each call occupies
    // ai-service for ~130-350ms (single uvicorn worker, no thread pool — a burst would stall every
    // other ai-service consumer, not just this check).
    // TODO: ai-service runs --workers 1 with synchronous handlers; consider a thread pool or
    // more workers before raising the check frequency any further.
    public async Task<InSessionVerifyResponse> VerifyInSessionAsync(
        Guid userId, Guid sessionId, Guid checkId, IFormFile frame, CancellationToken ct)
    {
        EnsureEnabled();
        if (frame is null || frame.Length == 0)
            throw new ArgumentException("A frame is required.");

        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new KeyNotFoundException("Session not found.");
        if (session.Status != "InProgress")
            throw new InvalidOperationException("Session is not in progress.");
        if (checkId == Guid.Empty)
            throw new InvalidOperationException("A verification check token is required.");
        if (!await _eventRepository.TryConsumeQuestionCheckAsync(
                checkId, sessionId, userId, VietnamTime.Now, ct))
            throw new InvalidOperationException("The verification check token is invalid, expired, or already used.");

        var maxStrikes = _configuration.GetValue("Biometrics:InSessionMaxStrikes", 3);
        var strictness = _configuration.GetValue("Biometrics:InSessionStrictness", "Strict");
        var noFaceStrikeThreshold = _configuration.GetValue("Biometrics:NoFaceStrikeThreshold", 3);
        var tabStrikeThreshold = _configuration.GetValue("Biometrics:TabStrikeThreshold", 3);

        var embedding = await GetStoredEmbeddingAsync(userId, ProfileBiometricsService.ModalityFace, ct);

        bool isStrike;
        bool noFaceDetected = false;
        bool qualityWarningDetected = false;
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
            detail = new
            {
                result.Score,
                result.Threshold,
                Reason = result.Match ? null : "Identity mismatch"
            };
            message = result.Match
                ? "Identity confirmed."
                : "The face in frame does not match your enrolled profile.";
        }
        catch (BiometricQualityException ex)
            when (ex.QualityCode == "multiple_faces" && ex.FaceCount is int faceCount)
        {
            isStrike = true;
            decision = "Flag";
            warningType = "MultiFace";
            detail = new { Reason = "Multiple faces detected", FaceCount = faceCount };
            message = $"Another person appears to be in frame with you ({faceCount} faces detected). " +
                      "If this continues, it will be treated as a possible attempt to get help during the interview.";
        }
        catch (BiometricQualityException ex)
        {
            isStrike = false;
            noFaceDetected = ex.QualityCode is null or "no_face";
            qualityWarningDetected = !noFaceDetected;
            decision = noFaceDetected ? "NoFace" : "Quality";
            warningType = ex.QualityCode switch
            {
                "image_too_blurry" => "ImageTooBlurry",
                "face_too_small" => "FaceTooSmall",
                "low_detection_confidence" => "LowDetectionConfidence",
                "cannot_decode_image" => "CannotDecode",
                _ => "NoFace",
            };
            detail = new { Reason = ex.Message, ex.QualityCode };
            message = warningType switch
            {
                "ImageTooBlurry" => "The camera image is too blurry — please hold still and improve the lighting.",
                "FaceTooSmall" => "Your face is too far from the camera — please move closer.",
                "LowDetectionConfidence" => "Your face is not clear enough — please face the camera directly.",
                "CannotDecode" => "The camera frame could not be processed. The check will be retried later.",
                _ => "We couldn't see your face — please make sure you're in frame.",
            };
        }

        await RecordEventAsync(sessionId, userId, "InSession", ProfileBiometricsService.ModalityFace,
            score: null, livenessPassed: null,
            matched: !isStrike && !noFaceDetected && !qualityWarningDetected,
            decision: decision, detail: detail, ct);

        var events = await _eventRepository.ListBySessionAsync(sessionId, "InSession", ct);
        var noFaceCount = events.Count(e => e.Modality == ProfileBiometricsService.ModalityFace && e.Decision == "NoFace");
        var effectiveStrikeCount = CheatingStrikeHelper.ComputeEffectiveStrikeCount(
            events, noFaceStrikeThreshold, tabStrikeThreshold);

        // This NoFace event just completed a fresh group of noFaceStrikeThreshold — it converts
        // into a strike right now, with the same weight as a Flag.
        var noFaceJustBecameStrike = noFaceDetected && noFaceCount % noFaceStrikeThreshold == 0;
        var strikeJustHappened = isStrike || noFaceJustBecameStrike;

        var shouldEndSession = CheatingStrikeHelper.ShouldEndSession(
            strikeJustHappened, strictness, effectiveStrikeCount, maxStrikes);

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
            message = $"Integrity warning limit reached ({displayStrikeCount}/{maxStrikes}). This interview has been ended.";
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
            Status = shouldEndSession ? "Cancelled" : session.Status,
            TerminationReason = shouldEndSession ? SessionCapabilityPolicy.IntegrityViolation : session.TerminationReason,
            CanGeneratePerformanceReport = false,
            CanSubmitInterviewFeedback = shouldEndSession,
            Passed = !isStrike && !noFaceDetected && !qualityWarningDetected,
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

    // Deliberately opt-in, unlike VerifyInSessionAsync (Face): there is no server-issued
    // mandatory token for voice, so this only runs when the candidate chooses to dictate an
    // answer via the mic (see Room's STT flow). Typing an answer skips this check entirely,
    // in every InteractionMode — that is accepted as-is; Face already provides the mandatory,
    // per-question identity check, and forcing spoken answers or adding a periodic mandatory
    // voice check would add UX friction and extra blocking ai-service calls (single worker)
    // for a narrow additional threat (remote voice coaching) not currently worth that cost.
    public async Task<VoiceAnswerVerifyResponse> VerifyVoiceAnswerAsync(
        Guid userId, Guid sessionId, Guid checkId, IFormFile audio, CancellationToken ct)
    {
        if (checkId == Guid.Empty)
            throw new ArgumentException("Voice check id is required.");
        if (audio is null || audio.Length == 0)
            throw new ArgumentException("An audio sample is required.");

        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new KeyNotFoundException("Session not found.");
        if (session.Status != "InProgress")
            throw new InvalidOperationException("Session is not in progress.");

        await using var audioStream = new MemoryStream();
        await audio.CopyToAsync(audioStream, ct);
        var audioBytes = audioStream.ToArray();
        var contentType = string.IsNullOrEmpty(audio.ContentType) ? "audio/webm" : audio.ContentType;
        var fileName = string.IsNullOrEmpty(audio.FileName) ? "answer.webm" : audio.FileName;

        var transcriptionTask = _aiServiceClient.TranscribeAsync(audioBytes, fileName, contentType, ct);
        var maxStrikes = _configuration.GetValue("Biometrics:InSessionMaxStrikes", 3);

        if (!_configuration.GetValue("Biometrics:Enabled", false))
        {
            return new VoiceAnswerVerifyResponse
            {
                Text = await transcriptionTask,
                VoiceChecked = false,
                MaxStrikes = maxStrikes,
            };
        }

        var voiceRow = await _biometricsRepository.GetAsync(
            userId, ProfileBiometricsService.ModalityVoice, ct);
        if (voiceRow is null)
        {
            return new VoiceAnswerVerifyResponse
            {
                Text = await transcriptionTask,
                VoiceChecked = false,
                MaxStrikes = maxStrikes,
                Message = "Voice verification is unavailable because no voice enrollment was found.",
            };
        }

        var existingEvent = await _eventRepository.GetIncidentAsync(checkId, ct);
        if (existingEvent is not null)
        {
            if (existingEvent.SessionId != sessionId || existingEvent.UserId != userId
                || existingEvent.Modality != ProfileBiometricsService.ModalityVoice)
            {
                throw new InvalidOperationException("Voice check id is already in use.");
            }

            return await BuildVoiceAnswerResponseAsync(
                sessionId, await transcriptionTask, existingEvent, true, maxStrikes, ct);
        }

        AiFaceVerifyResponse verification;
        try
        {
            using var probeStream = new MemoryStream(audioBytes, writable: false);
            var probe = new FormFile(probeStream, 0, audioBytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType,
            };
            verification = await _aiServiceClient.VerifyVoiceAsync(
                probe, EmbeddingCodec.ToFloats(voiceRow.Embedding), ct);
        }
        catch (BiometricQualityException)
        {
            return new VoiceAnswerVerifyResponse
            {
                Text = await transcriptionTask,
                VoiceChecked = false,
                MaxStrikes = maxStrikes,
                Message = "Voice verification could not assess this recording.",
            };
        }
        catch (Exception)
        {
            return new VoiceAnswerVerifyResponse
            {
                Text = await transcriptionTask,
                VoiceChecked = false,
                MaxStrikes = maxStrikes,
                Message = "Voice verification is temporarily unavailable.",
            };
        }

        var verificationEvent = new SessionVerificationEvent
        {
            SessionId = sessionId,
            UserId = userId,
            Phase = "InSession",
            Modality = ProfileBiometricsService.ModalityVoice,
            Score = verification.Score,
            Matched = verification.Match,
            Decision = verification.Match ? "Pass" : "Flag",
            IncidentId = checkId,
            Detail = JsonSerializer.Serialize(new
            {
                Reason = verification.Match ? null : "VoiceMismatch",
                verification.Threshold,
            }),
            CreatedAt = VietnamTime.Now,
        };

        var inserted = await _eventRepository.TryAddIncidentAsync(verificationEvent, ct);
        if (!inserted)
        {
            verificationEvent = await _eventRepository.GetIncidentAsync(checkId, ct)
                ?? throw new InvalidOperationException("Could not load the existing voice check.");
        }

        return await BuildVoiceAnswerResponseAsync(
            sessionId, await transcriptionTask, verificationEvent, !inserted, maxStrikes, ct);
    }

    private async Task<VoiceAnswerVerifyResponse> BuildVoiceAnswerResponseAsync(
        Guid sessionId,
        string text,
        SessionVerificationEvent verificationEvent,
        bool duplicate,
        int maxStrikes,
        CancellationToken ct)
    {
        var noFaceStrikeThreshold = _configuration.GetValue("Biometrics:NoFaceStrikeThreshold", 3);
        var tabStrikeThreshold = _configuration.GetValue("Biometrics:TabStrikeThreshold", 3);
        var strictness = _configuration.GetValue("Biometrics:InSessionStrictness", "Strict");
        var events = await _eventRepository.ListBySessionAsync(sessionId, "InSession", ct);
        var effectiveStrikeCount = CheatingStrikeHelper.ComputeEffectiveStrikeCount(
            events, noFaceStrikeThreshold, tabStrikeThreshold);
        var displayStrikeCount = Math.Min(effectiveStrikeCount, maxStrikes);
        var mismatch = verificationEvent.Decision == "Flag";
        var shouldEndSession = CheatingStrikeHelper.ShouldEndSession(
            mismatch, strictness, effectiveStrikeCount, maxStrikes);

        var message = mismatch
            ? "The recorded answer does not match your enrolled voice."
            : null;
        if (shouldEndSession)
        {
            await EndSessionForCheatingAsync(sessionId, ct);
            message = $"Integrity warning limit reached ({displayStrikeCount}/{maxStrikes}). This interview has been ended.";
        }

        return new VoiceAnswerVerifyResponse
        {
            Status = shouldEndSession ? "Cancelled" : "InProgress",
            TerminationReason = shouldEndSession ? SessionCapabilityPolicy.IntegrityViolation : null,
            CanGeneratePerformanceReport = false,
            CanSubmitInterviewFeedback = shouldEndSession,
            Text = text,
            VoiceChecked = true,
            VoiceMatched = !mismatch,
            Score = verificationEvent.Score,
            StrikeCount = displayStrikeCount,
            MaxStrikes = maxStrikes,
            ShouldEndSession = shouldEndSession,
            Duplicate = duplicate,
            Message = message,
        };
    }

    // Tab-switch / paste monitoring. The Room reports that the candidate left the interview tab
    // (or pasted into the answer box); this writes one audit row, Phase="InSession",
    // Decision="Flag". The Modality depends on EventType (see below):
    //   TabHidden / WindowBlur -> Modality="Tab"   -> NOT in CheatingStrikeHelper's direct-strike
    //                                                 whitelist — same treatment as NoFace: N of
    //                                                 them (Biometrics:TabStrikeThreshold) convert
    //                                                 into ONE effective strike instead of each
    //                                                 event counting as a full strike on its own
    //                                                 (see BuildTabStrikeResultAsync).
    //   Paste                  -> Modality="Paste" -> IS in the direct-strike whitelist (Phase 3):
    //                                                 every Paste is its own full strike, shares
    //                                                 the same pool as Face/Voice/Camera/Check
    //                                                 (see BuildPasteStrikeResultAsync).
    // Either way termination goes through the same CheatingStrikeHelper.ShouldEndSession the Face
    // check uses — not a re-copy of that predicate.
    //
    // Returns an outcome instead of throwing for the expected cases (feature off, not the owner,
    // session not running): every one of these has its own HTTP status in
    // SessionVerificationController, which the catch-all exception mapping there cannot express.
    public async Task<TabEventResponse> RecordTabEventAsync(
        Guid userId, Guid sessionId, RecordTabEventRequest req, CancellationToken ct)
    {
        if (!_configuration.GetValue("Biometrics:Enabled", false))
            return TabResult(TabOutcomeDisabled, req, "Biometric monitoring is disabled.");
        if (!_configuration.GetValue("Biometrics:TabSwitchEnabled", false))
            return TabResult(TabOutcomeDisabled, req, "Tab-switch monitoring is disabled.");

        // Same ownership + status guards as VerifyInSessionAsync, but reported as outcomes so the
        // controller can answer 403/409 rather than the 404/400 those exceptions map to.
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            return TabResult(TabOutcomeNotOwner, req, "Session not found.");
        if (session.Status != "InProgress")
            return TabResult(TabOutcomeNotInProgress, req, "Session is not in progress.");

        // Short flicker (an alt-tab that immediately comes back, a focus bounce from a browser
        // dialog) is not worth an audit row. Applies ONLY to the event types that actually measure
        // time away — "Paste" is instantaneous, DurationMs=0 is the correct value for it, and
        // filtering it out here would make the type unrecordable.
        var minAwayMs = _configuration.GetValue("Biometrics:TabSwitchMinAwayMs", 1500);
        if (DurationBearingTabEventTypes.Contains(req.EventType) && req.DurationMs < minAwayMs)
            return TabResult(TabOutcomeBelowThreshold, req,
                $"Ignored: shorter than the {minAwayMs}ms minimum.");

        if (!TabEventTypes.Contains(req.EventType))
            return TabResult(TabOutcomeInvalidEventType, req,
                $"EventType must be one of: {string.Join(", ", TabEventTypes)}.");
        // Without a real idempotency key the filtered unique index cannot dedupe (Guid.Empty is a
        // value, not NULL), so a retry would silently write a second row.
        if (req.IncidentId == Guid.Empty)
            return TabResult(TabOutcomeInvalidIncidentId, req, "IncidentId is required.");

        var modality = req.EventType == "Paste" ? ModalityPaste : ModalityTab;
        var inserted = await _eventRepository.TryAddIncidentAsync(
            new SessionVerificationEvent
            {
                SessionId = sessionId,
                UserId = userId,
                Phase = "InSession",
                Modality = modality,
                Matched = false,
                Decision = "Flag",
                // Exact shape the Room and any later reporting query rely on:
                // {"EventType":"TabHidden","DurationMs":1234}
                Detail = JsonSerializer.Serialize(new { req.EventType, req.DurationMs }),
                IncidentId = req.IncidentId,
                CreatedAt = VietnamTime.Now,
            },
            ct);

        if (!inserted)
            return TabResult(TabOutcomeDuplicate, req, "This event was already reported.");

        return modality == ModalityPaste
            ? await BuildPasteStrikeResultAsync(session, req, ct)
            : await BuildTabStrikeResultAsync(session, req, ct);
    }

    // Evaluates the shared strike/termination predicate for a just-recorded Paste event, exactly
    // the way VerifyInSessionAsync does for a Face strike — same helper, same maxStrikes/
    // strictness config keys, same EndSessionForCheatingAsync call. Only reached once per Paste
    // event that actually wrote a new row (RecordTabEventAsync already filtered out duplicates
    // before calling this).
    private async Task<TabEventResponse> BuildPasteStrikeResultAsync(
        InterviewSession session, RecordTabEventRequest req, CancellationToken ct)
    {
        var maxStrikes = _configuration.GetValue("Biometrics:InSessionMaxStrikes", 3);
        var strictness = _configuration.GetValue("Biometrics:InSessionStrictness", "Strict");
        var noFaceStrikeThreshold = _configuration.GetValue("Biometrics:NoFaceStrikeThreshold", 3);
        var tabStrikeThreshold = _configuration.GetValue("Biometrics:TabStrikeThreshold", 3);
        var events = await _eventRepository.ListBySessionAsync(session.SessionId, "InSession", ct);
        var effectiveStrikeCount = CheatingStrikeHelper.ComputeEffectiveStrikeCount(
            events, noFaceStrikeThreshold, tabStrikeThreshold);
        var displayStrikeCount = Math.Min(effectiveStrikeCount, maxStrikes);

        // strikeJustHappened is always true here: TryAddIncidentAsync just wrote a new
        // Decision="Flag" row for a whitelisted modality, so this call is unconditionally the
        // source of a fresh strike (unlike Face, there is no non-strike outcome for Paste).
        var shouldEndSession = CheatingStrikeHelper.ShouldEndSession(
            strikeJustHappened: true, strictness, effectiveStrikeCount, maxStrikes);

        var message = "Tab event recorded.";
        if (shouldEndSession)
        {
            await EndSessionForCheatingAsync(session.SessionId, ct);
            // Baked-in count on purpose, same reasoning as VerifyInSessionAsync — this exact
            // message is also shown standalone as the final chat bubble.
            message = $"Integrity warning limit reached ({displayStrikeCount}/{maxStrikes}). This interview has been ended.";
        }

        return new TabEventResponse
        {
            Recorded = true,
            Duplicated = false,
            Outcome = TabOutcomeRecorded,
            EventType = req.EventType,
            DurationMs = req.DurationMs,
            Message = message,
            StrikeCount = displayStrikeCount,
            MaxStrikes = maxStrikes,
            ShouldEndSession = shouldEndSession,
            Status = shouldEndSession ? "Cancelled" : session.Status,
            TerminationReason = shouldEndSession
                ? SessionCapabilityPolicy.IntegrityViolation
                : session.TerminationReason,
            CanGeneratePerformanceReport = false,
            CanSubmitInterviewFeedback = shouldEndSession,
        };
    }

    // Evaluates the NoFace-style accumulation for a just-recorded Tab/WindowBlur event: on its
    // own it is NOT a strike (Modality="Tab" stays outside CheatingStrikeHelper's direct-strike
    // whitelist), but every TabStrikeThreshold of them accumulated converts into one effective
    // strike — same weight as a Flag, same conversion shape as noFaceJustBecameStrike in
    // VerifyInSessionAsync. The termination predicate is therefore only evaluated on the exact
    // event that completes a fresh group; every other Tab/WindowBlur event returns progress info
    // only (TabCount/TabStrikeThreshold), with the strike fields left null.
    private async Task<TabEventResponse> BuildTabStrikeResultAsync(
        InterviewSession session, RecordTabEventRequest req, CancellationToken ct)
    {
        var tabStrikeThreshold = _configuration.GetValue("Biometrics:TabStrikeThreshold", 3);
        var events = await _eventRepository.ListBySessionAsync(session.SessionId, "InSession", ct);
        var tabCount = events.Count(e => e.Modality == ModalityTab && e.Decision == "Flag");

        // tabCount is always >= 1 here (a Tab row was just inserted above), so a zero remainder
        // means THIS event completed a fresh group of tabStrikeThreshold — the same "just became
        // a strike" shape as noFaceJustBecameStrike.
        var tabGroupProgress = tabCount % tabStrikeThreshold;
        var tabJustBecameStrike = tabGroupProgress == 0;

        var response = new TabEventResponse
        {
            Recorded = true,
            Duplicated = false,
            Outcome = TabOutcomeRecorded,
            EventType = req.EventType,
            DurationMs = req.DurationMs,
            Message = "Tab event recorded.",
            TabCount = tabGroupProgress,
            TabStrikeThreshold = tabStrikeThreshold,
            TabJustBecameStrike = tabJustBecameStrike,
        };

        if (!tabJustBecameStrike)
            return response;

        var maxStrikes = _configuration.GetValue("Biometrics:InSessionMaxStrikes", 3);
        var strictness = _configuration.GetValue("Biometrics:InSessionStrictness", "Strict");
        var noFaceStrikeThreshold = _configuration.GetValue("Biometrics:NoFaceStrikeThreshold", 3);
        var effectiveStrikeCount = CheatingStrikeHelper.ComputeEffectiveStrikeCount(
            events, noFaceStrikeThreshold, tabStrikeThreshold);
        var displayStrikeCount = Math.Min(effectiveStrikeCount, maxStrikes);
        var shouldEndSession = CheatingStrikeHelper.ShouldEndSession(
            strikeJustHappened: true, strictness, effectiveStrikeCount, maxStrikes);

        response.Message = "Repeated tab-switch detections counted as a cheating warning.";
        response.StrikeCount = displayStrikeCount;
        response.MaxStrikes = maxStrikes;
        response.ShouldEndSession = shouldEndSession;
        response.Status = shouldEndSession ? "Cancelled" : session.Status;
        response.TerminationReason = shouldEndSession
            ? SessionCapabilityPolicy.IntegrityViolation
            : session.TerminationReason;
        response.CanGeneratePerformanceReport = false;
        response.CanSubmitInterviewFeedback = shouldEndSession;

        if (shouldEndSession)
        {
            await EndSessionForCheatingAsync(session.SessionId, ct);
            // Same reasoning as VerifyInSessionAsync/BuildPasteStrikeResultAsync — this exact
            // message also stands alone as the final chat bubble.
            response.Message =
                $"Integrity warning limit reached ({displayStrikeCount}/{maxStrikes}). This interview has been ended.";
        }

        return response;
    }

    // Used for every outcome EXCEPT a recorded Tab/WindowBlur or Paste event (those go through
    // BuildTabStrikeResultAsync / BuildPasteStrikeResultAsync instead, which populate the strike
    // fields). All the nullable strike/progress fields are left unset here on purpose —
    // BelowThreshold/Duplicate/Disabled/error outcomes never wrote a row or evaluated any
    // predicate, so "no info" (null) is the honest answer for all of them.
    private static TabEventResponse TabResult(string outcome, RecordTabEventRequest req, string message) =>
        new()
        {
            Recorded = outcome == TabOutcomeRecorded,
            Duplicated = outcome == TabOutcomeDuplicate,
            Outcome = outcome,
            EventType = req.EventType,
            DurationMs = req.DurationMs,
            Message = message,
        };

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
        session.TerminationReason = SessionCapabilityPolicy.IntegrityViolation;
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

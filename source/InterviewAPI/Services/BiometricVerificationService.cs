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

    public async Task<EntryGateStatusResponse> GetEntryGateStatusAsync(
        Guid userId, Guid sessionId, CancellationToken ct)
    {
        if (!_configuration.GetValue("Biometrics:Enabled", false))
            return new EntryGateStatusResponse { Allowed = true, Reason = "Biometric verification is disabled." };

        await EnsureSessionOwnedAsync(userId, sessionId, ct);

        var faceRow = await _biometricsRepository.GetAsync(userId, ProfileBiometricsService.ModalityFace, ct);
        var voiceRow = await _biometricsRepository.GetAsync(userId, ProfileBiometricsService.ModalityVoice, ct);
        if (faceRow is null || voiceRow is null)
        {
            return new EntryGateStatusResponse
            {
                Allowed = false,
                Reason = "Please enroll both face and voice on your profile before entering the room.",
            };
        }

        var events = await _eventRepository.ListBySessionAsync(sessionId, "Entry", ct);
        var latestFace = events.FirstOrDefault(e => e.Modality == ProfileBiometricsService.ModalityFace);
        var latestVoice = events.FirstOrDefault(e => e.Modality == ProfileBiometricsService.ModalityVoice);
        var facePassed = latestFace?.Decision == "Pass";
        var voicePassed = latestVoice?.Decision == "Pass";

        // Prefer the fused decision when real calibration data (Milestone 3) is available;
        // otherwise fall back to requiring both modalities to independently pass, per the
        // master plan. Right now no real calibration.json exists, so this branch is dormant.
        var calibrationPath = _configuration.GetValue<string?>("Biometrics:CalibrationJsonPath");
        var fusionWeights = FusionDecisionHelper.TryLoadFusionWeights(calibrationPath);
        if (fusionWeights is not null && latestFace?.Score is double faceScore && latestVoice?.Score is double voiceScore)
        {
            var fused = FusionDecisionHelper.Decide(fusionWeights, faceScore, voiceScore);
            if (fused)
                return new EntryGateStatusResponse { Allowed = true, Reason = "Entry verification passed (fusion).", FacePassed = facePassed, VoicePassed = voicePassed };
        }
        else if (facePassed && voicePassed)
        {
            return new EntryGateStatusResponse { Allowed = true, Reason = "Entry verification already passed.", FacePassed = true, VoicePassed = true };
        }

        var maxRetries = _configuration.GetValue("Biometrics:EntryMaxRetries", 3);
        var failedAttempts = events.Count(e => e.Decision != "Pass");
        var retriesRemaining = Math.Max(0, maxRetries - failedAttempts);

        return new EntryGateStatusResponse
        {
            Allowed = false,
            Reason = retriesRemaining == 0 && failedAttempts > 0
                ? "Maximum verification attempts exceeded. Please contact support."
                : "Face and voice verification required before entering the room.",
            FacePassed = facePassed,
            VoicePassed = voicePassed,
            RetriesRemaining = retriesRemaining,
        };
    }

    public async Task<InSessionCheckDueResponse> IsInSessionCheckDueAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session is null)
            throw new KeyNotFoundException("Session not found.");

        var cadenceMinutes = _configuration.GetValue("Biometrics:InSessionCheckCadenceMinutes", 10.0);
        var events = await _eventRepository.ListBySessionAsync(sessionId, "InSession", ct);
        var anchor = events.FirstOrDefault()?.CreatedAt ?? session.StartedAt ?? session.CreatedAt;
        var elapsedMinutes = (VietnamTime.Now - anchor).TotalMinutes;

        // Deterministic-but-unpredictable target within [0.5x, 1.5x] cadence, reseeded only
        // when the anchor changes (i.e. once per completed window), so repeated polls within
        // the same window agree on the same due instant instead of flapping every call.
        var seed = HashCode.Combine(sessionId, anchor.Ticks);
        var rng = new Random(seed);
        var targetMinutes = cadenceMinutes * (0.5 + rng.NextDouble());

        return new InSessionCheckDueResponse
        {
            Due = elapsedMinutes >= targetMinutes,
            ElapsedMinutes = Math.Round(elapsedMinutes, 1),
            TargetMinutes = Math.Round(targetMinutes, 1),
        };
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

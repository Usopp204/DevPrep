using InterviewAPI.Clients;
using InterviewAPI.DTOs.AiService;
using InterviewAPI.DTOs.Biometrics;
using InterviewAPI.Helpers;
using InterviewAPI.Models;
using InterviewAPI.Repositories.Interfaces;
using InterviewAPI.Services.Interfaces;

namespace InterviewAPI.Services;

public class ProfileBiometricsService : IProfileBiometricsService
{
    public const string ModalityFace = "Face";
    public const string ModalityVoice = "Voice";

    // Outcomes of the one-time biometric enrollment bonus, surfaced to the client so a denied
    // bonus is no longer invisible outside InterviewAPI's own logs.
    public const string BonusStatusAwarded = "Awarded";
    public const string BonusStatusAlreadyAwarded = "AlreadyAwarded";
    public const string BonusStatusIdentityAlreadyClaimed = "IdentityAlreadyClaimed";
    public const string BonusStatusNotAwarded = "NotAwarded";
    public const string BonusStatusNotYetEligible = "NotYetEligible";

    private readonly IProfileBiometricsRepository _repository;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IPaymentApiClient _paymentApiClient;
    private readonly ILogger<ProfileBiometricsService> _logger;
    private readonly IConfiguration _configuration;

    public ProfileBiometricsService(
        IProfileBiometricsRepository repository,
        IAiServiceClient aiServiceClient,
        IPaymentApiClient paymentApiClient,
        ILogger<ProfileBiometricsService> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _aiServiceClient = aiServiceClient;
        _paymentApiClient = paymentApiClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<BiometricEnrollResponse> EnrollAsync(
        Guid userId, string modality, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("A sample file is required.");

        BiometricEmbeddingResult result = modality switch
        {
            ModalityFace => await _aiServiceClient.EmbedFaceAsync(file, ct),
            ModalityVoice => await _aiServiceClient.EmbedVoiceAsync(file, ct),
            _ => throw new ArgumentException($"Unknown modality '{modality}'."),
        };

        // Upsert: exactly one row per (userId, modality) — re-enrolling replaces the template.
        var existing = await _repository.GetAsync(userId, modality, ct);
        if (existing is null)
        {
            await _repository.AddAsync(new ProfileBiometric
            {
                UserId = userId,
                Modality = modality,
                Embedding = EmbeddingCodec.ToBytes(result.Embedding),
                Dim = result.Dim,
                ModelVersion = result.ModelVersion,
                Quality = result.Quality,
                CreatedAt = VietnamTime.Now,
                UpdatedAt = VietnamTime.Now,
            }, ct);
        }
        else
        {
            EnsureReEnrollmentMatches(existing, result, modality);
            existing.Embedding = EmbeddingCodec.ToBytes(result.Embedding);
            existing.Dim = result.Dim;
            existing.ModelVersion = result.ModelVersion;
            existing.Quality = result.Quality;
            existing.UpdatedAt = VietnamTime.Now;
            await _repository.SaveChangesAsync(ct);
        }

        var bonusStatus = await TryAwardEnrollmentBonusAsync(userId, modality, ct);

        return new BiometricEnrollResponse
        {
            Modality = modality,
            Dim = result.Dim,
            Quality = result.Quality,
            BonusStatus = bonusStatus,
        };
    }

    private void EnsureReEnrollmentMatches(
        ProfileBiometric existing,
        BiometricEmbeddingResult candidate,
        string modality)
    {
        if (existing.Dim != candidate.Dim || existing.ModelVersion != candidate.ModelVersion)
        {
            throw new InvalidOperationException(
                $"The enrolled {modality.ToLowerInvariant()} template uses an incompatible biometric model.");
        }

        var score = BiometricSimilarity.Cosine(
            EmbeddingCodec.ToFloats(existing.Embedding),
            candidate.Embedding);
        var thresholdKey = modality == ModalityFace
            ? "Biometrics:FaceReEnrollmentMatchThreshold"
            : "Biometrics:VoiceReEnrollmentMatchThreshold";
        var fallbackThreshold = modality == ModalityFace ? 0.35 : 0.25;
        var threshold = _configuration.GetValue(thresholdKey, fallbackThreshold);

        if (score < threshold)
            throw new BiometricIdentityMismatchException(modality, score, threshold);
    }

    // One-time bonus once BOTH Face and Voice are enrolled — checked by "does the OTHER modality
    // already exist", not "did I just enroll voice", so it fires correctly regardless of which
    // order the user enrolls in. Called after every enroll (including re-enrollment), which is
    // safe/intended: PaymentAPI's own idempotency guard (see AwardCreditAsync) is what actually
    // prevents awarding more than once, this check is just to skip the pointless call most of
    // the time (e.g. every re-enrollment after the first time both exist).
    private async Task<string> TryAwardEnrollmentBonusAsync(Guid userId, string justEnrolledModality, CancellationToken ct)
    {
        var otherModality = justEnrolledModality == ModalityFace ? ModalityVoice : ModalityFace;
        var other = await _repository.GetAsync(userId, otherModality, ct);
        if (other is null)
            return BonusStatusNotYetEligible; // still missing the other modality — not eligible yet.

        if (!await IsCanonicalBonusAccountAsync(userId, ct))
        {
            _logger.LogInformation(
                "Biometric enrollment bonus skipped because this identity belongs to an earlier account. userId={UserId}",
                userId);
            return BonusStatusIdentityAlreadyClaimed;
        }

        // Enroll is the primary action here — a PaymentAPI hiccup must never fail enrollment,
        // it only means no bonus this time (logged, not surfaced as an error to the user).
        try
        {
            var award = await _paymentApiClient.AwardCreditAsync(
                userId, CreditConfig.BiometricEnrollmentBonusCredits, userId,
                "Bonus: enrolled both Face and Voice biometrics", ct);

            if (!award.Success)
            {
                _logger.LogWarning(
                    "Biometric enrollment bonus award failed: userId={UserId}, reason={Reason}",
                    userId, award.Message);
                return BonusStatusNotAwarded;
            }

            // award.Success is also true for the idempotent no-op (user already had the bonus) —
            // only award.Awarded means THIS call is what granted it, which is what the caller
            // needs to decide whether to show a "you got a bonus!" notification.
            return award.Awarded ? BonusStatusAwarded : BonusStatusAlreadyAwarded;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Biometric enrollment bonus award threw: userId={UserId}", userId);
            return BonusStatusNotAwarded;
        }
    }

    private async Task<bool> IsCanonicalBonusAccountAsync(Guid userId, CancellationToken ct)
    {
        var faceTemplates = await _repository.ListByModalityAsync(ModalityFace, ct);
        var current = faceTemplates.FirstOrDefault(template => template.UserId == userId);
        if (current is null)
            return false;

        var currentEmbedding = EmbeddingCodec.ToFloats(current.Embedding);
        // Deliberately stricter than the re-enrollment thresholds: those tolerate the SAME person
        // across lighting/pose, this decides whether two DIFFERENT accounts are the same real
        // person — a loose threshold here produces false positives that silently deny a
        // legitimate user their bonus. Value is provisional pending Milestone 3 biometric
        // calibration (see ai-service/biometrics/calibrate.py).
        var threshold = _configuration.GetValue(
            "Biometrics:BonusDedupMatchThreshold", 0.5);

        var canonicalAccount = faceTemplates
            .Where(template =>
                template.Dim == current.Dim
                && template.ModelVersion == current.ModelVersion
                && BiometricSimilarity.Cosine(
                    currentEmbedding,
                    EmbeddingCodec.ToFloats(template.Embedding)) >= threshold)
            .OrderBy(template => template.CreatedAt)
            .ThenBy(template => template.Id)
            .FirstOrDefault();

        return canonicalAccount is not null && canonicalAccount.UserId == userId;
    }

    public async Task<BiometricStatusResponse> GetStatusAsync(Guid userId, CancellationToken ct)
    {
        var rows = await _repository.ListByUserAsync(userId, ct);
        var face = rows.FirstOrDefault(b => b.Modality == ModalityFace);
        var voice = rows.FirstOrDefault(b => b.Modality == ModalityVoice);
        return new BiometricStatusResponse
        {
            FaceEnrolled = face is not null,
            FaceUpdatedAt = face?.UpdatedAt,
            VoiceEnrolled = voice is not null,
            VoiceUpdatedAt = voice?.UpdatedAt,
        };
    }
}

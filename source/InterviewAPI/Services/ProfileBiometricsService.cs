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

    private readonly IProfileBiometricsRepository _repository;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IPaymentApiClient _paymentApiClient;
    private readonly ILogger<ProfileBiometricsService> _logger;

    public ProfileBiometricsService(
        IProfileBiometricsRepository repository,
        IAiServiceClient aiServiceClient,
        IPaymentApiClient paymentApiClient,
        ILogger<ProfileBiometricsService> logger)
    {
        _repository = repository;
        _aiServiceClient = aiServiceClient;
        _paymentApiClient = paymentApiClient;
        _logger = logger;
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
            existing.Embedding = EmbeddingCodec.ToBytes(result.Embedding);
            existing.Dim = result.Dim;
            existing.ModelVersion = result.ModelVersion;
            existing.Quality = result.Quality;
            existing.UpdatedAt = VietnamTime.Now;
            await _repository.SaveChangesAsync(ct);
        }

        var bonusAwarded = await TryAwardEnrollmentBonusAsync(userId, modality, ct);

        return new BiometricEnrollResponse
        {
            Modality = modality,
            Dim = result.Dim,
            Quality = result.Quality,
            BonusAwarded = bonusAwarded,
        };
    }

    // One-time bonus once BOTH Face and Voice are enrolled — checked by "does the OTHER modality
    // already exist", not "did I just enroll voice", so it fires correctly regardless of which
    // order the user enrolls in. Called after every enroll (including re-enrollment), which is
    // safe/intended: PaymentAPI's own idempotency guard (see AwardCreditAsync) is what actually
    // prevents awarding more than once, this check is just to skip the pointless call most of
    // the time (e.g. every re-enrollment after the first time both exist).
    private async Task<bool> TryAwardEnrollmentBonusAsync(Guid userId, string justEnrolledModality, CancellationToken ct)
    {
        var otherModality = justEnrolledModality == ModalityFace ? ModalityVoice : ModalityFace;
        var other = await _repository.GetAsync(userId, otherModality, ct);
        if (other is null)
            return false; // still missing the other modality — not eligible yet.

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
            }

            // award.Success is also true for the idempotent no-op (user already had the bonus) —
            // only award.Awarded means THIS call is what granted it, which is what the caller
            // needs to decide whether to show a "you got a bonus!" notification.
            return award.Awarded;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Biometric enrollment bonus award threw: userId={UserId}", userId);
            return false;
        }
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

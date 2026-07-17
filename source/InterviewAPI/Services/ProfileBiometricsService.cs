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

    public ProfileBiometricsService(IProfileBiometricsRepository repository, IAiServiceClient aiServiceClient)
    {
        _repository = repository;
        _aiServiceClient = aiServiceClient;
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

        return new BiometricEnrollResponse
        {
            Modality = modality,
            Dim = result.Dim,
            Quality = result.Quality,
        };
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

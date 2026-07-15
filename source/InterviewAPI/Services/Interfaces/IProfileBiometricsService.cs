using InterviewAPI.DTOs.Biometrics;

namespace InterviewAPI.Services.Interfaces;

public interface IProfileBiometricsService
{
    // modality: "Face" | "Voice" (plain-string convention). Upserts one row per (user, modality).
    Task<BiometricEnrollResponse> EnrollAsync(Guid userId, string modality, IFormFile file, CancellationToken ct);

    Task<BiometricStatusResponse> GetStatusAsync(Guid userId, CancellationToken ct);
}

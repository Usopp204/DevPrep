using InterviewAPI.Models;

namespace InterviewAPI.Repositories.Interfaces;

public interface IProfileBiometricsRepository
{
    // Tracked (for upsert): the one row for (userId, modality), or null if not enrolled yet.
    Task<ProfileBiometric?> GetAsync(Guid userId, string modality, CancellationToken ct);

    // Read-only list of everything the user has enrolled (status display).
    Task<List<ProfileBiometric>> ListByUserAsync(Guid userId, CancellationToken ct);

    // Read-only templates for one modality across all accounts. Used only to decide whether
    // a real person has already received the one-time biometric enrollment bonus.
    Task<List<ProfileBiometric>> ListByModalityAsync(string modality, CancellationToken ct);

    Task AddAsync(ProfileBiometric entity, CancellationToken ct);

    // Persists changes to an entity previously returned tracked by GetAsync.
    Task SaveChangesAsync(CancellationToken ct);
}

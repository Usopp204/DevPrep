using InterviewAPI.Models;
using InterviewAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Repositories;

public class ProfileBiometricsRepository : IProfileBiometricsRepository
{
    private readonly InterviewDbContext _db;

    public ProfileBiometricsRepository(InterviewDbContext db)
    {
        _db = db;
    }

    public Task<ProfileBiometric?> GetAsync(Guid userId, string modality, CancellationToken ct) =>
        _db.ProfileBiometrics
            .Where(b => b.UserId == userId && b.Modality == modality)
            .FirstOrDefaultAsync(ct);

    public Task<List<ProfileBiometric>> ListByUserAsync(Guid userId, CancellationToken ct) =>
        _db.ProfileBiometrics.AsNoTracking()
            .Where(b => b.UserId == userId)
            .ToListAsync(ct);

    public Task<List<ProfileBiometric>> ListByModalityAsync(string modality, CancellationToken ct) =>
        _db.ProfileBiometrics.AsNoTracking()
            .Where(b => b.Modality == modality)
            .ToListAsync(ct);

    public async Task AddAsync(ProfileBiometric entity, CancellationToken ct)
    {
        _db.ProfileBiometrics.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}

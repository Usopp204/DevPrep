using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Models;

// Biometrics additions kept in a separate partial file so the scaffolded
// InterviewDbContext.cs stays untouched.
public partial class InterviewDbContext
{
    public virtual DbSet<ProfileBiometric> ProfileBiometrics { get; set; }

    public virtual DbSet<SessionVerificationChallenge> SessionVerificationChallenges { get; set; }

    public virtual DbSet<SessionVerificationEvent> SessionVerificationEvents { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProfileBiometric>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
        });

        modelBuilder.Entity<SessionVerificationChallenge>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
        });

        modelBuilder.Entity<SessionVerificationEvent>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
        });
    }
}

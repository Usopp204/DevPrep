using Microsoft.EntityFrameworkCore;
using StatisticsAPI.Models;

namespace StatisticsAPI.Data;

public class StatisticsDbContext : DbContext
{
    public StatisticsDbContext(DbContextOptions<StatisticsDbContext> options) : base(options)
    {
    }

    public DbSet<MetricCounter> MetricCounters => Set<MetricCounter>();
    public DbSet<StatisticsSnapshot> StatisticsSnapshots => Set<StatisticsSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MetricCounter>(e =>
        {
            e.ToTable("MetricCounters");
            e.HasKey(x => x.MetricKey);
            e.Property(x => x.MetricKey).HasMaxLength(80);
            e.Property(x => x.MetricValue).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<StatisticsSnapshot>(e =>
        {
            e.ToTable("StatisticsSnapshots");
            e.HasKey(x => x.SnapshotId);
            // SnapshotId set thủ công trong code (DB có DEFAULT NEWSEQUENTIALID nhưng ta chủ động gán để upsert dễ).
            e.Property(x => x.SnapshotId).ValueGeneratedNever();
            e.HasIndex(x => x.SnapshotDate).IsUnique();
            e.Property(x => x.Revenue).HasColumnType("decimal(18,2)");
        });
    }
}

using Microsoft.EntityFrameworkCore;
using CvJdAPI.Models;

namespace CvJdAPI.Data;

public class DocumentDbContext : DbContext
{
    public DocumentDbContext(DbContextOptions<DocumentDbContext> options) : base(options)
    {
    }

    public DbSet<Cv> Cvs => Set<Cv>();
    public DbSet<JobDescription> JobDescriptions => Set<JobDescription>();
    public DbSet<MatchingScore> MatchingScores => Set<MatchingScore>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Cv>(e =>
        {
            e.ToTable("Cvs");
            e.HasKey(x => x.CvId);
            e.Property(x => x.CvId).HasDefaultValueSql("newsequentialid()");
        });

        modelBuilder.Entity<JobDescription>(e =>
        {
            e.ToTable("JobDescriptions");
            e.HasKey(x => x.JdId);
            e.Property(x => x.JdId).HasDefaultValueSql("newsequentialid()");
        });

        modelBuilder.Entity<MatchingScore>(e =>
        {
            e.ToTable("MatchingScores");
            e.HasKey(x => x.MatchingScoreId);
            e.Property(x => x.MatchingScoreId).HasDefaultValueSql("newsequentialid()");
            e.Property(x => x.Score).HasColumnType("decimal(5,2)");
        });
    }
}

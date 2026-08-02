using EvaluationAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaluationAPI.Data;

public class EvaluationDbContext : DbContext
{
    public EvaluationDbContext(DbContextOptions<EvaluationDbContext> options) : base(options)
    {
    }

    public virtual DbSet<AnswerEvaluation> AnswerEvaluations { get; set; }

    public virtual DbSet<FeedbackReport> FeedbackReports { get; set; }

    public virtual DbSet<PerformanceMetric> PerformanceMetrics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnswerEvaluation>(entity =>
        {
            entity.HasKey(e => e.EvaluationId);
            entity.Property(e => e.EvaluationId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.GeneratedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
        });

        modelBuilder.Entity<FeedbackReport>(entity =>
        {
            entity.HasKey(e => e.ReportId);
            entity.HasIndex(e => e.SessionId).IsUnique();
            entity.Property(e => e.ReportId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.GeneratedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
        });

        modelBuilder.Entity<PerformanceMetric>(entity =>
        {
            entity.HasKey(e => e.MetricId);
            entity.Property(e => e.MetricId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.RecordedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
        });

        base.OnModelCreating(modelBuilder);
    }
}

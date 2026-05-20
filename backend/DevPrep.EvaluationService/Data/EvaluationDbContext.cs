using Microsoft.EntityFrameworkCore;

namespace DevPrep.EvaluationService.Data;

public class EvaluationDbContext : DbContext
{
    public EvaluationDbContext(DbContextOptions<EvaluationDbContext> options) : base(options)
    {
    }

    // TODO: DbSet<Entity> — ví dụ: public DbSet<Evaluation> Evaluations => Set<Evaluation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TODO (Rule 23): Hardcode Seed Data bằng Fluent API (HasData)
        // modelBuilder.Entity<...>().HasData(...);
    }
}

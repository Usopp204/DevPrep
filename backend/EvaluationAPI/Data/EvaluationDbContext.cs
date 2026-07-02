using Microsoft.EntityFrameworkCore;

namespace EvaluationAPI.Data;

public class EvaluationDbContext : DbContext
{
    public EvaluationDbContext(DbContextOptions<EvaluationDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}


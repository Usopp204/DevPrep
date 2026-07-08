using Microsoft.EntityFrameworkCore;

namespace StatisticsAPI.Data;

public class StatisticsDbContext : DbContext
{
    public StatisticsDbContext(DbContextOptions<StatisticsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}


using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Data;

public class FeedbackDbContext : DbContext
{
    public FeedbackDbContext(DbContextOptions<FeedbackDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}


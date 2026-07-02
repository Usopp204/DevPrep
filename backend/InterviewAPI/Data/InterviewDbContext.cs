using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Data;

public class InterviewDbContext : DbContext
{
    public InterviewDbContext(DbContextOptions<InterviewDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}


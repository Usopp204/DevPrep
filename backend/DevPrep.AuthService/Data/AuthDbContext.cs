using Microsoft.EntityFrameworkCore;

namespace DevPrep.AuthService.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    // TODO: DbSet<Entity> — ví dụ: public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TODO (Rule 23): Hardcode Seed Data bằng Fluent API (HasData)
        // modelBuilder.Entity<...>().HasData(...);
    }
}

using Microsoft.EntityFrameworkCore;

namespace DevPrep.AdminService.Data;

public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options)
    {
    }

    // TODO: DbSet<Entity> — ví dụ: public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TODO (Rule 23): Hardcode Seed Data bằng Fluent API (HasData)
        // modelBuilder.Entity<...>().HasData(...);
    }
}

using Microsoft.EntityFrameworkCore;

namespace DevPrep.PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    // TODO: DbSet<Entity> — ví dụ: public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TODO (Rule 23): Hardcode Seed Data bằng Fluent API (HasData)
        // modelBuilder.Entity<...>().HasData(...);
    }
}

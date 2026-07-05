using Microsoft.EntityFrameworkCore;
using PaymentAPI.Models;

namespace PaymentAPI.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<CreditPackage> CreditPackages { get; set; }

    public DbSet<Order> Orders { get; set; }

    public DbSet<CreditWallet> CreditWallets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CreditPackage>(entity =>
        {
            entity.HasKey(e => e.PackageId);
            entity.Property(e => e.PackageId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.BonusCredits).HasDefaultValue(0);
            entity.Property(e => e.Currency).HasDefaultValue("VND");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId);
            entity.Property(e => e.OrderId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Currency).HasDefaultValue("VND");
            entity.Property(e => e.PaymentMethod).HasDefaultValue("VNPay");
            entity.Property(e => e.Status).HasDefaultValue("Pending");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<CreditWallet>(entity =>
        {
            entity.HasKey(e => e.WalletId);
            entity.Property(e => e.WalletId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Balance).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });
    }
}


using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace UserAPI.Models;

public partial class UserDbContext : DbContext
{
    public UserDbContext()
    {
    }

    public UserDbContext(DbContextOptions<UserDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<UserProfile> UserProfiles { get; set; }

    // Guard IsConfigured: khi chạy thật, options đã được DI cấu hình từ appsettings (có Encrypt=False)
    // nên KHÔNG ghi đè. Chuỗi hard-code chỉ dùng cho design-time (scaffold/migration).
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=.;Database=DevPrep_User;User Id=sa;Password=123;TrustServerCertificate=True;");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.Property(e => e.UserId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

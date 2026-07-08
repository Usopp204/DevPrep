using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace AuthAPI.Models;

public partial class AuthDbContext : DbContext
{
    public AuthDbContext()
    {
    }

    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<AccountBlockHistory> AccountBlockHistories { get; set; }

    public virtual DbSet<BiometricVerification> BiometricVerifications { get; set; }

    public virtual DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; }

    public virtual DbSet<LoginAudit> LoginAudits { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<SsoIdentity> SsoIdentities { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Chỉ dùng connection string mặc định khi chưa được cấu hình từ DI (ví dụ chạy
        // EF Core tooling lúc design-time). Khi chạy app, cấu hình trong Program.cs sẽ thắng.
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=.;Database=DevPrep_Auth;User Id=sa;Password=123;TrustServerCertificate=True;");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(e => e.AccountId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Role).HasDefaultValue("User");
            entity.Property(e => e.Status).HasDefaultValue("Pending");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<AccountBlockHistory>(entity =>
        {
            entity.Property(e => e.BlockHistoryId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Account).WithMany(p => p.AccountBlockHistories).HasConstraintName("FK_BlockHist_Account");
        });

        modelBuilder.Entity<BiometricVerification>(entity =>
        {
            entity.Property(e => e.BiometricId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Status).HasDefaultValue("Pending");

            entity.HasOne(d => d.Account).WithMany(p => p.BiometricVerifications).HasConstraintName("FK_Bio_Account");
        });

        modelBuilder.Entity<EmailVerificationToken>(entity =>
        {
            entity.Property(e => e.TokenId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Account).WithMany(p => p.EmailVerificationTokens).HasConstraintName("FK_EmailVer_Account");
        });

        modelBuilder.Entity<LoginAudit>(entity =>
        {
            entity.Property(e => e.LoginAuditId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Method).HasDefaultValue("Password");
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.Property(e => e.TokenId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Account).WithMany(p => p.PasswordResetTokens).HasConstraintName("FK_PwdReset_Account");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.Property(e => e.RefreshTokenId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Account).WithMany(p => p.RefreshTokens).HasConstraintName("FK_Refresh_Account");
        });

        modelBuilder.Entity<SsoIdentity>(entity =>
        {
            entity.Property(e => e.SsoIdentityId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Account).WithMany(p => p.SsoIdentities).HasConstraintName("FK_Sso_Account");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace NotificationAPI.Models;

public partial class NotificationDbContext : DbContext
{
    public NotificationDbContext()
    {
    }

    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<EmailLog> EmailLogs { get; set; }

    public virtual DbSet<EmailTemplate> EmailTemplates { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    // Guard IsConfigured: khi chạy thật, options đã được DI cấu hình từ appsettings (có Encrypt=False)
    // nên KHÔNG ghi đè. Chuỗi hard-code chỉ dùng cho design-time (scaffold/migration).
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=.;Database=DevPrep_Notification;User Id=sa;Password=123;TrustServerCertificate=True;");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.Property(e => e.EmailLogId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Status).HasDefaultValue("Pending");
        });

        modelBuilder.Entity<EmailTemplate>(entity =>
        {
            entity.Property(e => e.TemplateId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(e => e.NotificationId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Type).HasDefaultValue("System");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

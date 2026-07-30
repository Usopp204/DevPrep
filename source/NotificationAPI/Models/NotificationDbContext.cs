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

    public virtual DbSet<Announcement> Announcements { get; set; }

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

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.Property(e => e.AnnouncementId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour, 7, sysutcdatetime()))");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(dateadd(hour, 7, sysutcdatetime()))");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

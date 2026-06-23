using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Models;

public partial class FeedbackDbContext : DbContext
{
    public FeedbackDbContext()
    {
    }

    public FeedbackDbContext(DbContextOptions<FeedbackDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<FeedbackReply> FeedbackReplies { get; set; }

    public virtual DbSet<InterviewFeedback> InterviewFeedbacks { get; set; }

    public virtual DbSet<SystemFeedback> SystemFeedbacks { get; set; }

    // Guard IsConfigured: khi chạy thật, options đã được DI cấu hình từ appsettings (có Encrypt=False)
    // nên KHÔNG ghi đè. Chuỗi hard-code chỉ dùng cho design-time (scaffold/migration) và có sẵn Encrypt=False.
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=.;Database=DevPrep_Feedback;User Id=sa;Password=123;TrustServerCertificate=True;Encrypt=False");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeedbackReply>(entity =>
        {
            entity.Property(e => e.ReplyId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackReplies).HasConstraintName("FK_Reply_Feedback");
        });

        modelBuilder.Entity<InterviewFeedback>(entity =>
        {
            entity.Property(e => e.InterviewFeedbackId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
        });

        modelBuilder.Entity<SystemFeedback>(entity =>
        {
            entity.Property(e => e.FeedbackId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Category).HasDefaultValue("General");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
            entity.Property(e => e.Status).HasDefaultValue("New");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

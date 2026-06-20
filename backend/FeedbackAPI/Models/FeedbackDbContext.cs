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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=.;Database=DevPrep_Feedback;User Id=sa;Password=123;TrustServerCertificate=True;");

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

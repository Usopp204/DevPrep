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

    public virtual DbSet<FeedbackReport> FeedbackReports { get; set; }

    public virtual DbSet<InterviewFeedback> InterviewFeedbacks { get; set; }

    public virtual DbSet<SystemFeedback> SystemFeedbacks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeedbackReply>(entity =>
        {
            entity.Property(e => e.ReplyId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackReplies).HasConstraintName("FK_Reply_Feedback");
        });

        modelBuilder.Entity<FeedbackReport>(entity =>
        {
            entity.ToTable("FeedbackReports", table =>
            {
                table.HasCheckConstraint(
                    "CK_FeedbackReports_FeedbackType",
                    "[FeedbackType] IN (N'System', N'Interview')");
                table.HasCheckConstraint(
                    "CK_FeedbackReports_ExactlyOneTarget",
                    "([InterviewFeedbackId] IS NOT NULL AND [SystemFeedbackId] IS NULL) OR ([InterviewFeedbackId] IS NULL AND [SystemFeedbackId] IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_FeedbackReports_TypeMatchesTarget",
                    "([FeedbackType] = N'Interview' AND [InterviewFeedbackId] IS NOT NULL AND [SystemFeedbackId] IS NULL) OR ([FeedbackType] = N'System' AND [SystemFeedbackId] IS NOT NULL AND [InterviewFeedbackId] IS NULL)");
                table.HasCheckConstraint(
                    "CK_FeedbackReports_ReasonCode",
                    "[ReasonCode] IN (N'HarassmentOrPersonalAttack', N'HateOrDiscrimination', N'ThreatsOrViolence', N'SexualOrInappropriateContent', N'SpamOrMisleading', N'PrivacyOrPersonalInformation', N'OffTopicOrIrrelevant', N'Other')");
                table.HasCheckConstraint(
                    "CK_FeedbackReports_Status",
                    "[Status] IN (N'Pending', N'Dismissed', N'ResolvedByFeedbackDeletion')");
            });

            entity.Property(e => e.FeedbackReportId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
            entity.Property(e => e.Status).HasDefaultValue("Pending");

            entity.HasIndex(e => new { e.ReporterUserId, e.InterviewFeedbackId }, "UX_FeedbackReports_Reporter_Interview")
                .IsUnique()
                .HasFilter("[InterviewFeedbackId] IS NOT NULL");
            entity.HasIndex(e => new { e.ReporterUserId, e.SystemFeedbackId }, "UX_FeedbackReports_Reporter_System")
                .IsUnique()
                .HasFilter("[SystemFeedbackId] IS NOT NULL");

            entity.HasOne(e => e.InterviewFeedback)
                .WithMany(e => e.FeedbackReports)
                .HasForeignKey(e => e.InterviewFeedbackId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_FeedbackReports_InterviewFeedbacks");
            entity.HasOne(e => e.SystemFeedback)
                .WithMany(e => e.FeedbackReports)
                .HasForeignKey(e => e.SystemFeedbackId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_FeedbackReports_SystemFeedbacks");
        });

        modelBuilder.Entity<InterviewFeedback>(entity =>
        {
            entity.Property(e => e.InterviewFeedbackId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
            entity.Property(e => e.ModerationStatus).HasDefaultValue("Pending");
            entity.Property(e => e.IsVisible).HasDefaultValue(false);
        });

        modelBuilder.Entity<SystemFeedback>(entity =>
        {
            entity.Property(e => e.FeedbackId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Category).HasDefaultValue("General");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
            entity.Property(e => e.Status).HasDefaultValue("New");
            entity.Property(e => e.ModerationStatus).HasDefaultValue("Pending");
            entity.Property(e => e.IsVisible).HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Models;

public partial class InterviewDbContext : DbContext
{
    public InterviewDbContext()
    {
    }

    public InterviewDbContext(DbContextOptions<InterviewDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    public virtual DbSet<InterviewRoom> InterviewRooms { get; set; }

    public virtual DbSet<InterviewSession> InterviewSessions { get; set; }

    public virtual DbSet<SessionQuestion> SessionQuestions { get; set; }

    public virtual DbSet<UserRoomTourStatus> UserRoomTourStatuses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.Property(e => e.MessageId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.MessageType).HasDefaultValue("Text");
            entity.Property(e => e.SentAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");

            entity.HasOne(d => d.SessionQuestion).WithMany(p => p.ChatMessages).HasConstraintName("FK_Msg_SessionQuestion");
        });

        modelBuilder.Entity<InterviewRoom>(entity =>
        {
            entity.Property(e => e.RoomId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.InitializedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
            entity.Property(e => e.Status).HasDefaultValue("Open");

            entity.HasOne(d => d.Session).WithOne(p => p.InterviewRoom).HasConstraintName("FK_Room_Session");
        });

        modelBuilder.Entity<InterviewSession>(entity =>
        {
            entity.Property(e => e.SessionId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
            entity.Property(e => e.DurationMinutes).HasDefaultValue(30);
            entity.Property(e => e.InteractionMode).HasDefaultValue("Text");
            entity.Property(e => e.Level).HasDefaultValue("Junior");
            entity.Property(e => e.QuestionType).HasDefaultValue("Mixed");
            entity.Property(e => e.Status).HasDefaultValue("Configured");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
        });

        modelBuilder.Entity<SessionQuestion>(entity =>
        {
            entity.Property(e => e.SessionQuestionId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.IsAiGenerated).HasDefaultValue(true);

            entity.HasOne(d => d.Session).WithMany(p => p.SessionQuestions).HasConstraintName("FK_SQ_Session");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

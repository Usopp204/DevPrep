using Microsoft.EntityFrameworkCore;
using QuestionAPI.Models;

namespace QuestionAPI.Data;

public class QuestionBankDbContext : DbContext
{
    public QuestionBankDbContext(DbContextOptions<QuestionBankDbContext> options) : base(options)
    {
    }

    public DbSet<Question> Questions { get; set; }
    public DbSet<QuestionCategory> QuestionCategories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<QuestionCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId);
            entity.Property(e => e.CategoryId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.QuestionId);
            entity.Property(e => e.QuestionId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.QuestionType).HasDefaultValue("Theory");
            entity.Property(e => e.Level).HasDefaultValue("Junior");
            entity.Property(e => e.Difficulty).HasDefaultValue("Medium");
            entity.Property(e => e.Status).HasDefaultValue("Active");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Questions)
                  .HasForeignKey(e => e.CategoryId)
                  .HasConstraintName("FK_Q_Category")
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}



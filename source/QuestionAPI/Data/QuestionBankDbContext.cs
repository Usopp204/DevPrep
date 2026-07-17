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
            entity.HasKey(e => e.CategoryId).HasName("PK_QuestionCategories");
            entity.Property(e => e.CategoryId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Category).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(dateadd(hour, 7, sysutcdatetime()))");
            entity.HasIndex(e => e.Category, "UQ_QuestionCategories_Name").IsUnique();
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.QuestionId);
            entity.Property(e => e.QuestionId).HasDefaultValueSql("(newsequentialid())");
            
            //Question and Level xóa không còn trong database 
            //entity.Property(e => e.QuestionType).HasDefaultValue("Theory");
            //entity.Property(e => e.Level).HasDefaultValue("Junior");


            entity.Property(e => e.Difficulty).HasDefaultValue("Medium");
            entity.Property(e => e.Status).HasDefaultValue("Active");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Position).HasMaxLength(50).IsRequired(false);

            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Questions)
                  .HasForeignKey(e => e.CategoryId)
                  .HasConstraintName("FK_Q_Category")
                  .OnDelete(DeleteBehavior.NoAction);
        });
    }
}



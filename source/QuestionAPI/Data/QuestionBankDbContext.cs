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
    public DbSet<Position> Positions { get; set; }

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

            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Questions)
                  .HasForeignKey(e => e.CategoryId)
                  .HasConstraintName("FK_Q_Category")
                  .OnDelete(DeleteBehavior.NoAction);

            // Position chuẩn hoá thành FK (cột text cũ đã bỏ — Migration_2026-07-24).
            entity.HasOne(e => e.Position)
                  .WithMany(p => p.Questions)
                  .HasForeignKey(e => e.PositionId)
                  .HasConstraintName("FK_Q_Position")
                  .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasKey(e => e.PositionId).HasName("PK_Positions");
            entity.Property(e => e.PositionId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour, 7, sysutcdatetime()))");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(dateadd(hour, 7, sysutcdatetime()))");
            // Unique tên chỉ áp cho dòng chưa xoá — xoá mềm xong vẫn tạo lại được tên cũ.
            entity.HasIndex(e => e.Name, "UQ_Positions_Name").IsUnique().HasFilter("[IsDeleted] = 0");
        });
    }
}



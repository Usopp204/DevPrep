using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace NewsAPI.Models;

public partial class NewsDbContext : DbContext
{
    public NewsDbContext()
    {
    }

    public NewsDbContext(DbContextOptions<NewsDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<NewsArticle> NewsArticles { get; set; }

    public virtual DbSet<NewsCategory> NewsCategories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NewsArticle>(entity =>
        {
            entity.Property(e => e.NewsId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
            entity.Property(e => e.Status).HasDefaultValue("Draft");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");

            entity.HasOne(d => d.Category).WithMany(p => p.NewsArticles).HasConstraintName("FK_News_Category");
        });

        modelBuilder.Entity<NewsCategory>(entity =>
        {
            entity.Property(e => e.CategoryId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(dateadd(hour,(7),sysutcdatetime()))");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

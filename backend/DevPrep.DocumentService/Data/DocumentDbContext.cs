using Microsoft.EntityFrameworkCore;

namespace DevPrep.DocumentService.Data;

public class DocumentDbContext : DbContext
{
    public DocumentDbContext(DbContextOptions<DocumentDbContext> options) : base(options)
    {
    }

    // TODO: DbSet<Entity> — ví dụ: public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TODO (Rule 23): Hardcode Seed Data bằng Fluent API (HasData)
        // modelBuilder.Entity<...>().HasData(...);
    }
}

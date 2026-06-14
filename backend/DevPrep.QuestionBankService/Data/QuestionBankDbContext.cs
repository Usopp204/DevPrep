using Microsoft.EntityFrameworkCore;

namespace DevPrep.QuestionBankService.Data;

public class QuestionBankDbContext : DbContext
{
    public QuestionBankDbContext(DbContextOptions<QuestionBankDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}

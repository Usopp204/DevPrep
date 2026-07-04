using Microsoft.EntityFrameworkCore;
using QuestionAPI.Data;
using QuestionAPI.Models;
using QuestionAPI.Repositories.Interfaces;

namespace QuestionAPI.Repositories;

public class AdminQuestionRepository : IAdminQuestionRepository
{
    private readonly QuestionBankDbContext _db;

    public AdminQuestionRepository(QuestionBankDbContext db)
    {
        _db = db;
    }

    public IQueryable<Question> Query()
    {
        return _db.Questions.AsQueryable();
    }

    public Task<Question?> GetByIdAsync(Guid id)
    {
        return _db.Questions
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.QuestionId == id);
    }

    public Task<Question?> GetTrackedAsync(Guid id)
    {
        return _db.Questions
            .FirstOrDefaultAsync(x => x.QuestionId == id);
    }

    public async Task AddAsync(Question q)
    {
        await _db.Questions.AddAsync(q);
    }

    public Task SaveAsync()
    {
        return _db.SaveChangesAsync();
    }
}
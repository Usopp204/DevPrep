using Microsoft.EntityFrameworkCore;
using QuestionAPI.Data;
using QuestionAPI.Models;
using QuestionAPI.Repositories.Interfaces;

namespace QuestionAPI.Repositories;

public class PositionRepository : IPositionRepository
{
    private readonly QuestionBankDbContext _db;

    public PositionRepository(QuestionBankDbContext db)
    {
        _db = db;
    }

    public IQueryable<Position> Query()
    {
        return _db.Positions.AsQueryable();
    }

    public Task<Position?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _db.Positions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PositionId == id, cancellationToken);
    }

    public Task<Position?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _db.Positions
            .FirstOrDefaultAsync(p => p.PositionId == id, cancellationToken);
    }

    public Task<Position?> FindActiveByNameAsync(string name, Guid? excludedId, CancellationToken cancellationToken)
    {
        return _db.Positions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Name == name && !p.IsDeleted &&
                    (!excludedId.HasValue || p.PositionId != excludedId.Value),
                cancellationToken);
    }

    public Task<int> CountQuestionsAsync(Guid positionId, CancellationToken cancellationToken)
    {
        return _db.Questions.CountAsync(
            q => q.PositionId == positionId && !q.IsDeleted, cancellationToken);
    }

    public async Task AddAsync(Position position, CancellationToken cancellationToken)
    {
        await _db.Positions.AddAsync(position, cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}

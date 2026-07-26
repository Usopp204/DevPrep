using Microsoft.EntityFrameworkCore;
using QuestionAPI.Data;
using QuestionAPI.DTOs.TechStacks;
using QuestionAPI.Models;
using QuestionAPI.Repositories.Interfaces;

namespace QuestionAPI.Repositories;

public class TechStackRepository : ITechStackRepository
{
    private readonly QuestionBankDbContext _db;

    public TechStackRepository(QuestionBankDbContext db)
    {
        _db = db;
    }

    public async Task<TechStackPageData> GetPageAsync(
        TechStackQueryDto query,
        CancellationToken cancellationToken)
    {
        var categories = _db.Database.SqlQuery<TechStackListItemDto>($"""
            SELECT
                CAST(ROW_NUMBER() OVER (
                    ORDER BY [CreatedAt] ASC, [CategoryId] ASC
                ) AS int) AS [DisplayId],
                [CategoryId] AS [TechStackId],
                [Category] AS [Name],
                [Description],
                [IsActive],
                [CreatedAt]
            FROM [QuestionCategories]
            """);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search}%";
            categories = categories.Where(category =>
                EF.Functions.Like(category.Name, pattern) ||
                (category.Description != null && EF.Functions.Like(category.Description, pattern)));
        }

        categories = query.Status switch
        {
            TechStackStatusFilter.Active => categories.Where(category => category.IsActive),
            TechStackStatusFilter.Inactive => categories.Where(category => !category.IsActive),
            _ => categories
        };

        var totalRecords = await categories.CountAsync(cancellationToken);
        var items = await categories
            .OrderByDescending(category => category.CreatedAt)
            .ThenByDescending(category => category.TechStackId)
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new TechStackPageData(items, totalRecords);
    }

    public Task<QuestionCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _db.QuestionCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(category => category.CategoryId == id, cancellationToken);
    }

    public Task<QuestionCategory?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _db.QuestionCategories
            .FirstOrDefaultAsync(category => category.CategoryId == id, cancellationToken);
    }

    public Task<QuestionCategory?> FindByNameAsync(
        string name,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        return _db.QuestionCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                category => category.Category == name &&
                    (!excludedId.HasValue || category.CategoryId != excludedId.Value),
                cancellationToken);
    }

    public async Task AddAsync(QuestionCategory category, CancellationToken cancellationToken)
    {
        await _db.QuestionCategories.AddAsync(category, cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}

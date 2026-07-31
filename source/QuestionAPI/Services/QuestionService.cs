using Microsoft.EntityFrameworkCore;
using QuestionAPI.Data;
using QuestionAPI.DTOs;

namespace QuestionAPI.Services;

public class QuestionService : IQuestionService
{
    private readonly QuestionBankDbContext _db;

    public QuestionService(QuestionBankDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<QuestionListItemDto>> GetListAsync(
        int page, int pageSize,
        string? keyword,
        string? questionType,
        string? level,
        string? difficulty,
        string? status,
        Guid? categoryId)
    {
        var query = _db.Questions
            .Include(q => q.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            query = query.Where(q => EF.Functions.Like(q.QuestionText, pattern));
        }

        if (!string.IsNullOrWhiteSpace(difficulty))
            query = query.Where(q => q.Difficulty == difficulty);

        if (string.Equals(status, "Deleted", StringComparison.OrdinalIgnoreCase))
            query = query.Where(q => q.IsDeleted);
        else
        {
            query = query.Where(q => !q.IsDeleted);
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(q => q.Status == status);
        }

        if (categoryId.HasValue)
            query = query.Where(q => q.CategoryId == categoryId.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new QuestionListItemDto
            {
                QuestionId   = q.QuestionId,
                CategoryId   = q.CategoryId,
                Category     = q.Category != null ? q.Category.Category : null,
                Question     = q.QuestionText,
                Difficulty   = q.Difficulty,
                Position     = q.Position != null ? q.Position.Name : null,
                Status       = q.Status,
                IsDeleted    = q.IsDeleted,
                CreatedAt    = q.CreatedAt,
            })
            .ToListAsync();

        return new PagedResult<QuestionListItemDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    public async Task<QuestionDetailDto?> GetByIdAsync(Guid id)
    {
        var q = await _db.Questions
            .Include(x => x.Category)
            .Include(x => x.Position)
            .FirstOrDefaultAsync(x => x.QuestionId == id);

        if (q is null) return null;

        return new QuestionDetailDto
        {
            QuestionId   = q.QuestionId,
            CategoryId     = q.CategoryId,
            Category       = q.Category?.Category,
            Question       = q.QuestionText,
            Difficulty     = q.Difficulty,
            Answer         = q.Answer,
            Position       = q.Position?.Name,
            Status         = q.Status,
            IsDeleted      = q.IsDeleted,
            CreatedBy      = q.CreatedBy,
            DeletedBy      = q.DeletedBy,
            DeletedAt      = q.DeletedAt,
            CreatedAt      = q.CreatedAt,
            UpdatedAt      = q.UpdatedAt,
        };
    }

    public Task<List<QuestionCategoryDto>> GetCategoriesAsync()
    {
        return _db.QuestionCategories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.Category)
            .Select(category => new QuestionCategoryDto
            {
                CategoryId = category.CategoryId,
                Category = category.Category,
                Description = category.Description,
                IsActive = category.IsActive
            })
            .ToListAsync();
    }
}

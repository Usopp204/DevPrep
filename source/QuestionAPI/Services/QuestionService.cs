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
            .Where(q => !q.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(q =>
                q.Content.ToLower().Contains(kw) ||
                (q.Tags != null && q.Tags.ToLower().Contains(kw)) ||
                (q.TechStack != null && q.TechStack.ToLower().Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(questionType))
            query = query.Where(q => q.QuestionType == questionType);

        if (!string.IsNullOrWhiteSpace(level))
            query = query.Where(q => q.Level == level);

        if (!string.IsNullOrWhiteSpace(difficulty))
            query = query.Where(q => q.Difficulty == difficulty);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(q => q.Status == status);

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
                CategoryName = q.Category != null ? q.Category.Name : null,
                Content      = q.Content,
                QuestionType = q.QuestionType,
                Level        = q.Level,
                Difficulty   = q.Difficulty,
                TechStack    = q.TechStack,
                Tags         = q.Tags,
                Status       = q.Status,
                CreatedAt    = q.CreatedAt,
                UpdatedAt    = q.UpdatedAt,
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
            .FirstOrDefaultAsync(x => x.QuestionId == id && !x.IsDeleted);

        if (q is null) return null;

        return new QuestionDetailDto
        {
            QuestionId   = q.QuestionId,
            Category     = q.Category is null ? null : new QuestionCategoryDto
            {
                CategoryId  = q.Category.CategoryId,
                Name        = q.Category.Name,
                Description = q.Category.Description,
                IsActive    = q.Category.IsActive,
            },
            Content        = q.Content,
            QuestionType   = q.QuestionType,
            Level          = q.Level,
            Difficulty     = q.Difficulty,
            ExpectedAnswer = q.ExpectedAnswer,
            TechStack      = q.TechStack,
            Tags           = q.Tags,
            Status         = q.Status,
            CreatedBy      = q.CreatedBy,
            CreatedAt      = q.CreatedAt,
            UpdatedAt      = q.UpdatedAt,
        };
    }
}

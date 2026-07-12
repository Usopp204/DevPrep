using Microsoft.EntityFrameworkCore;
using QuestionAPI.Data;
using QuestionAPI.DTOs;
using QuestionAPI.Models;
using QuestionAPI.Repositories.Interfaces;
using QuestionAPI.Services.Interfaces;
using QuestionAPI.Helpers;
namespace QuestionAPI.Services;

public class AdminQuestionService : IAdminQuestionService
{
    private readonly IAdminQuestionRepository _repo;
    private readonly QuestionBankDbContext _db;

    public AdminQuestionService(IAdminQuestionRepository repo, QuestionBankDbContext db)
    {
        _repo = repo;
        _db = db;
    }

    public async Task<AdminPagedResultDto<AdminQuestionListItemDto>> GetAsync(AdminQuestionQueryDto q)
    {
        var query = ApplyFilter(_repo.Query(), q);

        return await PagingAsync(query, q);
    }

    public async Task<AdminPagedResultDto<AdminQuestionListItemDto>> SearchAsync(AdminQuestionQueryDto q)
    {
        var query = ApplyFilter(_repo.Query(), q);

        return await PagingAsync(query, q);
    }

    public async Task<AdminPagedResultDto<AdminQuestionListItemDto>> FilterAsync(AdminQuestionQueryDto q)
    {
        var query = ApplyFilter(_repo.Query(), q);

        return await PagingAsync(query, q);
    }

    public async Task<AdminQuestionDetailDto?> GetDetailAsync(Guid id)
    {
        var question = await _repo.GetByIdAsync(id);

        if (question == null)
        {
            return null;
        }

        return MapDetail(question);
    }

    public async Task<AdminQuestionDetailDto> CreateAsync(AdminCreateQuestionDto dto, Guid? adminId)
    {
        if (dto.CategoryId.HasValue)
        {
            var categoryExists = await _db.QuestionCategories
                .AnyAsync(x => x.CategoryId == dto.CategoryId.Value && x.IsActive);

            if (!categoryExists)
            {
                throw new Exception("Question category does not exist or has been disabled.");
            }
        }

        var now =  VietnamTime.Now;

        var question = new Question
        {
            QuestionId = Guid.NewGuid(),
            CategoryId = dto.CategoryId,
            Content = dto.Content.Trim(),
            QuestionType = string.IsNullOrWhiteSpace(dto.QuestionType) ? "Theory" : dto.QuestionType.Trim(),
            Level = string.IsNullOrWhiteSpace(dto.Level) ? "Junior" : dto.Level.Trim(),
            Difficulty = string.IsNullOrWhiteSpace(dto.Difficulty) ? "Medium" : dto.Difficulty.Trim(),
            ExpectedAnswer = dto.ExpectedAnswer,
            TechStack = dto.TechStack,
            Tags = dto.Tags,
            // Bổ sung
            Position = string.IsNullOrWhiteSpace(dto.Position)? null: dto.Position.Trim(),

            Status = "Active",
            IsDeleted = false,
            CreatedBy = adminId,
            DeletedAt = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repo.AddAsync(question);
        await _repo.SaveAsync();

        var categoryName = await GetCategoryNameAsync(question.CategoryId);

        return MapDetail(question, categoryName);
    }

    public async Task<AdminQuestionDetailDto?> UpdateAsync(Guid id, AdminUpdateQuestionDto dto, Guid? adminId)
    {
        var question = await _repo.GetTrackedAsync(id);

        if (question == null)
        {
            return null;
        }

        if (question.IsDeleted)
        {
            throw new Exception("This question has been soft-deleted. Please restore it before updating.");
        }

        if (dto.Content != null && string.IsNullOrWhiteSpace(dto.Content))
        {
            throw new Exception("Question content cannot be empty.");
        }

        if (dto.CategoryId.HasValue)
        {
            var categoryExists = await _db.QuestionCategories
                .AnyAsync(x => x.CategoryId == dto.CategoryId.Value && x.IsActive);

            if (!categoryExists)
            {
                throw new Exception("Question category does not exist or has been disabled.");
            }
        }

        if (dto.CategoryId.HasValue)
        {
            question.CategoryId = dto.CategoryId;
        }

        if (dto.Content != null)
        {
            question.Content = dto.Content.Trim();
        }

        if (dto.QuestionType != null)
        {
            question.QuestionType = dto.QuestionType.Trim();
        }

        if (dto.Level != null)
        {
            question.Level = dto.Level.Trim();
        }

        if (dto.Difficulty != null)
        {
            question.Difficulty = dto.Difficulty.Trim();
        }

        if (dto.ExpectedAnswer != null)
        {
            question.ExpectedAnswer = dto.ExpectedAnswer;
        }

        if (dto.TechStack != null)
        {
            question.TechStack = dto.TechStack;
        }

        if (dto.Tags != null)
        {
            question.Tags = dto.Tags;
        }

        if (dto.Position != null)
        {
            question.Position = string.IsNullOrWhiteSpace(dto.Position)
                ? null
                : dto.Position.Trim();
        }

        if (dto.Status != null)
        {
            question.Status = dto.Status.Trim();
        }

        question.UpdatedAt =  VietnamTime.Now;

        await _repo.SaveAsync();

        var categoryName = await GetCategoryNameAsync(question.CategoryId);

        return MapDetail(question, categoryName);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? adminId)
    {
        var question = await _repo.GetTrackedAsync(id);

        if (question == null || question.IsDeleted)
        {
            return false;
        }

        question.IsDeleted = true;
        question.DeletedAt =  VietnamTime.Now;
        question.DeletedBy = adminId;   // <-- THÊM
        question.UpdatedAt =  VietnamTime.Now;

        await _repo.SaveAsync();

        return true;
    }

    public async Task<bool> RestoreAsync(Guid id)
    {
        var question = await _repo.GetTrackedAsync(id);

        if (question == null || !question.IsDeleted)
        {
            return false;
        }

        question.IsDeleted = false;
        question.DeletedAt = null;
        question.DeletedBy = null;   // <-- THÊM
        question.Status = "Active";
        question.UpdatedAt =  VietnamTime.Now;

        await _repo.SaveAsync();

        return true;
    }

    public async Task<List<AdminCategoryDto>> GetAllCategoriesAsync()
    {
        return await _db.QuestionCategories
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new AdminCategoryDto
            {
                CategoryId = x.CategoryId,
                Name = x.Name
            })
            .ToListAsync();
    }

    private IQueryable<Question> ApplyFilter(IQueryable<Question> query, AdminQuestionQueryDto f)
    {
        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var keyword = f.Keyword.Trim().ToLower();

            query = query.Where(x =>
                x.Content.ToLower().Contains(keyword) ||
                (x.Tags != null && x.Tags.ToLower().Contains(keyword)) ||
                (x.TechStack != null && x.TechStack.ToLower().Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(f.Level))
        {
            var level = f.Level.Trim().ToLower();
            query = query.Where(x => x.Level.ToLower() == level);
        }

        if (!string.IsNullOrWhiteSpace(f.QuestionType))
        {
            var type = f.QuestionType.Trim().ToLower();
            query = query.Where(x => x.QuestionType.ToLower() == type);
        }

        if (!string.IsNullOrWhiteSpace(f.Difficulty))
        {
            var difficulty = f.Difficulty.Trim().ToLower();
            query = query.Where(x => x.Difficulty.ToLower() == difficulty);
        }

        if (!string.IsNullOrWhiteSpace(f.TechStack))
        {
            var techStack = f.TechStack.Trim().ToLower();

            query = query.Where(x =>
                x.TechStack != null &&
                x.TechStack.ToLower().Contains(techStack));
        }

        if (f.CategoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == f.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(f.Status))
        {
            var status = f.Status.Trim();

            if (status.Equals("Deleted", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.IsDeleted);
            }
            else
            {
                query = query.Where(x =>
                    !x.IsDeleted &&
                    x.Status.ToLower() == status.ToLower());
            }
        }

        return query;
    }

    private async Task<AdminPagedResultDto<AdminQuestionListItemDto>> PagingAsync(
        IQueryable<Question> query,
        AdminQuestionQueryDto f)
    {
        if (f.PageIndex <= 0)
        {
            f.PageIndex = 1;
        }

        if (f.PageSize <= 0)
        {
            f.PageSize = 10;
        }

        var totalRecords = await query.CountAsync();

        var totalPages = (int)Math.Ceiling((double)totalRecords / f.PageSize);

        var items = await query
    .Include(x => x.Category)
    .OrderByDescending(x => x.CreatedAt)
    .Skip((f.PageIndex - 1) * f.PageSize)
    .Take(f.PageSize)
    .Select(x => new AdminQuestionListItemDto
    {
        QuestionId = x.QuestionId,
        CategoryId = x.CategoryId,
        CategoryName = x.Category != null ? x.Category.Name : null,
        Content = x.Content,
        QuestionType = x.QuestionType,
        Level = x.Level,
        Difficulty = x.Difficulty,
        TechStack = x.TechStack,
        Tags = x.Tags,
        Position = x.Position,
        Status = x.Status,
        IsDeleted = x.IsDeleted,
        DeletedAt = x.DeletedAt,
        DeletedBy = x.DeletedBy,   // <-- SỬA, bỏ null
        CreatedAt = x.CreatedAt
    })
    .ToListAsync();

        return new AdminPagedResultDto<AdminQuestionListItemDto>
        {
            PageIndex = f.PageIndex,
            PageSize = f.PageSize,
            TotalRecords = totalRecords,
            TotalPages = totalPages,
            Items = items
        };
    }

    private async Task<string?> GetCategoryNameAsync(Guid? categoryId)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        return await _db.QuestionCategories
            .Where(x => x.CategoryId == categoryId.Value)
            .Select(x => x.Name)
            .FirstOrDefaultAsync();
    }

    private AdminQuestionDetailDto MapDetail(Question x, string? categoryName = null)
    {
        return new AdminQuestionDetailDto
        {
            QuestionId = x.QuestionId,
            CategoryId = x.CategoryId,
            CategoryName = categoryName ?? x.Category?.Name,
            Content = x.Content,
            QuestionType = x.QuestionType,
            Level = x.Level,
            Difficulty = x.Difficulty,
            ExpectedAnswer = x.ExpectedAnswer,
            TechStack = x.TechStack,
            Tags = x.Tags,
            Position = x.Position,
            Status = x.Status,
            IsDeleted = x.IsDeleted,
            CreatedBy = x.CreatedBy,
            DeletedBy = x.DeletedBy,
            DeletedAt = x.DeletedAt,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        };
    }
}
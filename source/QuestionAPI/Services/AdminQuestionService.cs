using Microsoft.EntityFrameworkCore;
using QuestionAPI.Data;
using QuestionAPI.DTOs;
using QuestionAPI.Helpers;
using QuestionAPI.Models;
using QuestionAPI.Repositories.Interfaces;
using QuestionAPI.Services.Interfaces;

namespace QuestionAPI.Services;

public class AdminQuestionService : IAdminQuestionService
{
    private const int MaxBrowsePages = 30;
    private const int DefaultPageSize = 10;
    private const int MaximumPageSize = 100;

    private static readonly HashSet<string> AllowedStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Active",
            "Draft",
            "Archived",
            "Inactive"
        };

    private readonly IAdminQuestionRepository _repo;
    private readonly QuestionBankDbContext _db;
    private readonly IQuestionIndexClient _indexClient;

    public AdminQuestionService(
        IAdminQuestionRepository repo,
        QuestionBankDbContext db,
        IQuestionIndexClient indexClient)
    {
        _repo = repo;
        _db = db;
        _indexClient = indexClient;
    }

    public async Task<AdminPagedResultDto<AdminQuestionListItemDto>> GetAsync(
        AdminQuestionQueryDto queryDto)
    {
        var query = ApplyFilter(
            _repo.Query(),
            queryDto
        );

        return await PagingAsync(
            query,
            queryDto
        );
    }

    public async Task<AdminPagedResultDto<AdminQuestionListItemDto>> SearchAsync(
        AdminQuestionQueryDto queryDto)
    {
        var query = ApplyFilter(
            _repo.Query(),
            queryDto
        );

        return await PagingAsync(
            query,
            queryDto
        );
    }

    public async Task<AdminPagedResultDto<AdminQuestionListItemDto>> FilterAsync(
        AdminQuestionQueryDto queryDto)
    {
        var query = ApplyFilter(
            _repo.Query(),
            queryDto
        );

        return await PagingAsync(
            query,
            queryDto
        );
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

    public async Task<AdminQuestionDetailDto> CreateAsync(
        AdminCreateQuestionDto dto,
        Guid? adminId)
    {
        if (!dto.CategoryId.HasValue)
        {
            throw new Exception("CategoryId is required.");
        }

        var categoryExists = await _db.QuestionCategories
            .AsNoTracking()
            .AnyAsync(x =>
                x.CategoryId == dto.CategoryId.Value &&
                x.IsActive
            );

        if (!categoryExists)
        {
            throw new Exception(
                "Question category does not exist or has been disabled."
            );
        }

        if (string.IsNullOrWhiteSpace(dto.Question))
        {
            throw new Exception("Question cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(dto.Difficulty))
        {
            throw new Exception("Difficulty cannot be empty.");
        }

        var now = VietnamTime.Now;

        var question = new Question
        {
            QuestionId = Guid.NewGuid(),
            CategoryId = dto.CategoryId,

            QuestionText = dto.Question.Trim(),
            Difficulty = dto.Difficulty.Trim(),

            Answer = NormalizeNullable(dto.Answer),
            Position = NormalizeNullable(dto.Position),

            Status = "Active",
            IsDeleted = false,

            CreatedBy = adminId,
            DeletedBy = null,
            DeletedAt = null,

            CreatedAt = now,
            UpdatedAt = now
        };

        await _repo.AddAsync(question);
        await _repo.SaveAsync();

        var categoryName = await GetCategoryAsync(question.CategoryId);

        // Đồng bộ sang ChromaDB
        await SyncQuestionIndexAsync(question, categoryName);

        return MapDetail(
            question,
            categoryName
        );
    }

    public async Task<AdminQuestionDetailDto?> UpdateAsync(
        Guid id,
        AdminUpdateQuestionDto dto,
        Guid? adminId)
    {
        var question = await _repo.GetTrackedAsync(id);

        if (question == null)
        {
            return null;
        }

        if (question.IsDeleted)
        {
            throw new Exception(
                "This question has been soft-deleted. " +
                "Please restore it before updating."
            );
        }

        if (dto.Question != null &&
            string.IsNullOrWhiteSpace(dto.Question))
        {
            throw new Exception("Question cannot be empty.");
        }

        if (dto.Difficulty != null &&
            string.IsNullOrWhiteSpace(dto.Difficulty))
        {
            throw new Exception("Difficulty cannot be empty.");
        }

        if (dto.CategoryId.HasValue)
        {
            var categoryExists = await _db.QuestionCategories
                .AsNoTracking()
                .AnyAsync(x =>
                    x.CategoryId == dto.CategoryId.Value &&
                    x.IsActive
                );

            if (!categoryExists)
            {
                throw new Exception(
                    "Question category does not exist or has been disabled."
                );
            }

            question.CategoryId = dto.CategoryId;
        }

        if (dto.Question != null)
        {
            question.QuestionText = dto.Question.Trim();
        }

        if (dto.Difficulty != null)
        {
            question.Difficulty = dto.Difficulty.Trim();
        }

        if (dto.Answer != null)
        {
            question.Answer = NormalizeNullable(dto.Answer);
        }

        if (dto.Position != null)
        {
            question.Position = NormalizeNullable(dto.Position);
        }

        if (dto.Status != null)
        {
            var status = dto.Status.Trim();

            if (string.IsNullOrWhiteSpace(status))
            {
                throw new Exception("Status cannot be empty.");
            }

            if (!AllowedStatuses.Contains(status))
            {
                throw new Exception(
                    "Status must be Active, Draft, Archived or Inactive."
                );
            }

            question.Status = NormalizeStatus(status);
        }

        question.UpdatedAt = VietnamTime.Now;

        await _repo.SaveAsync();

        var categoryName = await GetCategoryAsync(question.CategoryId);

        // Đồng bộ hóa thông minh (Thêm/Sửa hoặc Xoá khỏi ChromaDB dựa vào Status mới)
        await SyncQuestionIndexAsync(question, categoryName);

        return MapDetail(
            question,
            categoryName
        );
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        Guid? adminId)
    {
        var question = await _repo.GetTrackedAsync(id);

        if (question == null || question.IsDeleted)
        {
            return false;
        }

        var now = VietnamTime.Now;

        question.IsDeleted = true;
        question.DeletedAt = now;
        question.DeletedBy = adminId;
        question.UpdatedAt = now;

        await _repo.SaveAsync();

        // Gọi thẳng Client Delete vì câu hỏi đã bị soft-delete
        await _indexClient.DeleteAsync(id);

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
        question.DeletedBy = null;
        question.Status = "Active";
        question.UpdatedAt = VietnamTime.Now;

        await _repo.SaveAsync();

        var categoryName = await GetCategoryAsync(question.CategoryId);

        // Đồng bộ lại vào ChromaDB
        await SyncQuestionIndexAsync(question, categoryName);

        return true;
    }

    public async Task<List<AdminCategoryDto>> GetAllCategoriesAsync()
    {
        return await _db.QuestionCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Category)
            .Select(x => new AdminCategoryDto
            {
                CategoryId = x.CategoryId,
                Category = x.Category
            })
            .ToListAsync();
    }

    public async Task<AdminReindexResultDto> ReindexAllQuestionsAsync()
    {
        // Lấy toàn bộ câu hỏi Active và Chưa bị xoá
        var activeQuestions = await _db.Questions
            .AsNoTracking()
            .Where(q => !q.IsDeleted && q.Status == "Active")
            .ToListAsync();

        // Lấy danh sách Categories để tối ưu hóa việc map tên thay vì truy vấn lặp trong DB
        var categories = await _db.QuestionCategories
            .AsNoTracking()
            .ToDictionaryAsync(c => c.CategoryId, c => c.Category);

        int successCount = 0;
        int failCount = 0;

        foreach (var question in activeQuestions)
        {
            string? categoryName = null;
            if (question.CategoryId.HasValue)
            {
                categories.TryGetValue(question.CategoryId.Value, out categoryName);
            }

            var isSuccess = await _indexClient.UpsertAsync(question, categoryName);
            if (isSuccess)
            {
                successCount++;
            }
            else
            {
                failCount++;
            }
        }

        return new AdminReindexResultDto
        {
            TotalProcessed = activeQuestions.Count,
            SuccessCount = successCount,
            FailCount = failCount
        };
    }

    private IQueryable<Question> ApplyFilter(
        IQueryable<Question> query,
        AdminQuestionQueryDto filter)
    {
        query = query.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();

            query = query.Where(x =>
                x.QuestionText.Contains(keyword) ||
                (
                    x.Position != null &&
                    x.Position.Contains(keyword)
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(filter.Difficulty))
        {
            var difficulty = filter.Difficulty.Trim();

            query = query.Where(x =>
                x.Difficulty == difficulty
            );
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(x =>
                x.CategoryId == filter.CategoryId.Value
            );
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim();

            if (status.Equals(
                "Deleted",
                StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    x.IsDeleted
                );
            }
            else
            {
                query = query.Where(x =>
                    !x.IsDeleted &&
                    x.Status == status
                );
            }
        }

        return query;
    }

    private async Task<AdminPagedResultDto<AdminQuestionListItemDto>> PagingAsync(
        IQueryable<Question> query,
        AdminQuestionQueryDto filter)
    {
        if (filter.PageIndex <= 0)
        {
            filter.PageIndex = 1;
        }

        if (filter.PageSize <= 0)
        {
            filter.PageSize = DefaultPageSize;
        }

        if (filter.PageSize > MaximumPageSize)
        {
            filter.PageSize = MaximumPageSize;
        }

        var maximumRecords = MaxBrowsePages * filter.PageSize;

        var limitedKeys = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.QuestionId)
            .Select(x => new
            {
                x.QuestionId,
                x.CreatedAt
            })
            .Take(maximumRecords)
            .ToListAsync();

        var totalRecords = limitedKeys.Count;

        var totalPages = totalRecords == 0
            ? 0
            : (int)Math.Ceiling((double)totalRecords / filter.PageSize);

        if (totalPages > MaxBrowsePages)
        {
            totalPages = MaxBrowsePages;
        }

        if (totalPages > 0 && filter.PageIndex > totalPages)
        {
            filter.PageIndex = totalPages;
        }

        if (filter.PageIndex <= 0)
        {
            filter.PageIndex = 1;
        }

        var pageKeys = limitedKeys
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        var pageIds = pageKeys
            .Select(x => x.QuestionId)
            .ToList();

        List<AdminQuestionListItemDto> questionItems;

        if (pageIds.Count == 0)
        {
            questionItems = new List<AdminQuestionListItemDto>();
        }
        else
        {
            questionItems = await _db.Questions
                .AsNoTracking()
                .Where(x => pageIds.Contains(x.QuestionId))
                .Select(x => new AdminQuestionListItemDto
                {
                    QuestionId = x.QuestionId,
                    CategoryId = x.CategoryId,
                    Category = null,
                    Question = x.QuestionText,
                    Difficulty = x.Difficulty,
                    Position = x.Position,
                    Status = x.Status,
                    IsDeleted = x.IsDeleted,
                    DeletedAt = x.DeletedAt,
                    DeletedBy = x.DeletedBy,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

        var orderMap = pageKeys
            .Select((x, index) => new
            {
                x.QuestionId,
                Index = index
            })
            .ToDictionary(x => x.QuestionId, x => x.Index);

        questionItems = questionItems
            .OrderBy(x => orderMap.TryGetValue(x.QuestionId, out var index) ? index : int.MaxValue)
            .ToList();

        var categoryIds = questionItems
            .Where(x => x.CategoryId.HasValue)
            .Select(x => x.CategoryId!.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, string> categoryDictionary;

        if (categoryIds.Count == 0)
        {
            categoryDictionary = new Dictionary<Guid, string>();
        }
        else
        {
            categoryDictionary = await _db.QuestionCategories
                .AsNoTracking()
                .Where(x => categoryIds.Contains(x.CategoryId))
                .ToDictionaryAsync(x => x.CategoryId, x => x.Category);
        }

        foreach (var item in questionItems)
        {
            if (!item.CategoryId.HasValue)
            {
                continue;
            }

            if (categoryDictionary.TryGetValue(item.CategoryId.Value, out var categoryName))
            {
                item.Category = categoryName;
            }
        }

        return new AdminPagedResultDto<AdminQuestionListItemDto>
        {
            PageIndex = filter.PageIndex,
            PageSize = filter.PageSize,
            TotalRecords = totalRecords,
            TotalPages = totalPages,
            Items = questionItems
        };
    }

    private async Task<string?> GetCategoryAsync(Guid? categoryId)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        return await _db.QuestionCategories
            .AsNoTracking()
            .Where(x => x.CategoryId == categoryId.Value)
            .Select(x => x.Category)
            .FirstOrDefaultAsync();
    }

    private static AdminQuestionDetailDto MapDetail(
        Question question,
        string? category = null)
    {
        return new AdminQuestionDetailDto
        {
            QuestionId = question.QuestionId,
            CategoryId = question.CategoryId,
            Category = category ?? question.Category?.Category,
            Question = question.QuestionText,
            Difficulty = question.Difficulty,
            Answer = question.Answer,
            Position = question.Position,
            Status = question.Status,
            IsDeleted = question.IsDeleted,
            CreatedBy = question.CreatedBy,
            DeletedBy = question.DeletedBy,
            DeletedAt = question.DeletedAt,
            CreatedAt = question.CreatedAt,
            UpdatedAt = question.UpdatedAt
        };
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeStatus(string status)
    {
        if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
        {
            return "Active";
        }

        if (status.Equals("Draft", StringComparison.OrdinalIgnoreCase))
        {
            return "Draft";
        }

        if (status.Equals("Archived", StringComparison.OrdinalIgnoreCase))
        {
            return "Archived";
        }

        return "Inactive";
    }

    private Task SyncQuestionIndexAsync(Question question, string? category)
    {
        var shouldIndex = !question.IsDeleted &&
                          string.Equals(question.Status, "Active", StringComparison.OrdinalIgnoreCase);

        return shouldIndex
            ? _indexClient.UpsertAsync(question, category)
            : _indexClient.DeleteAsync(question.QuestionId);
    }
}
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
    // private const int MaxBrowsePages = 30;
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

        // Validate Status giống UpdateAsync
        var status = string.IsNullOrWhiteSpace(dto.Status)
            ? "Active"
            : dto.Status.Trim();

        if (!AllowedStatuses.Contains(status))
        {
            throw new Exception(
                "Status must be Active, Draft, Archived or Inactive."
            );
        }

        status = NormalizeStatus(status);


        if (dto.PositionId.HasValue)
        {
            var positionExists = await _db.Positions
                .AsNoTracking()
                .AnyAsync(p =>
                    p.PositionId == dto.PositionId.Value &&
                    !p.IsDeleted
                );

            if (!positionExists)
            {
                throw new Exception(
                    "Position does not exist or has been deleted."
                );
            }
        }

        var now = VietnamTime.Now;

        var question = new Question
        {
            QuestionId = Guid.NewGuid(),
            CategoryId = dto.CategoryId,

            QuestionText = dto.Question.Trim(),
            Difficulty = dto.Difficulty.Trim(),

            Answer = NormalizeNullable(dto.Answer),
            PositionId = dto.PositionId,

            Status = status,   // dùng giá trị đã validate từ dto, không hardcode nữa
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

        if (dto.PositionId.HasValue)
        {
            var positionExists = await _db.Positions
                .AsNoTracking()
                .AnyAsync(p =>
                    p.PositionId == dto.PositionId.Value &&
                    !p.IsDeleted
                );

            if (!positionExists)
            {
                throw new Exception(
                    "Position does not exist or has been deleted."
                );
            }

            question.PositionId = dto.PositionId.Value;
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
        // Lấy toàn bộ câu hỏi Active và Chưa bị xoá (kèm Position để index tên position)
        var activeQuestions = await _db.Questions
            .AsNoTracking()
            .Include(q => q.Position)
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

    // ------------------------------------------------------------
    // ĐÃ SỬA: chuyển từ x.Field.ToLower().Contains(keyword) (non-sargable,
    // buộc quét toàn bảng, dễ timeout trên dữ liệu lớn / gây lỗi ngẫu nhiên)
    // sang EF.Functions.Like (dịch ra LIKE chuẩn SQL, tận dụng collation
    // case-insensitive sẵn có và thân thiện với index hơn).
    // ------------------------------------------------------------
    private IQueryable<Question> ApplyFilter(
    IQueryable<Question> query,
    AdminQuestionQueryDto filter)
    {
        query = query.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            var pattern = $"%{keyword}%";

            query = query.Where(x =>
                EF.Functions.Like(x.QuestionText, pattern)
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
            var categoryId = filter.CategoryId.Value;

            query = query.Where(x =>
                x.CategoryId == categoryId
            );
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim();

            if (status.Equals(
                "Deleted",
                StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.IsDeleted);
            }
            else
            {
                query = query.Where(x =>
                    !x.IsDeleted &&
                    x.Status == status
                );
            }
        }
        else
        {
            // All Statuses nhưng không lấy dữ liệu đã soft-delete
            query = query.Where(x => !x.IsDeleted);
        }

        return query;
    }
    //-------------------------

    private async Task<AdminPagedResultDto<AdminQuestionListItemDto>> PagingAsync(
    IQueryable<Question> query,
    AdminQuestionQueryDto filter)
    {
        // Chuẩn hóa PageIndex
        if (filter.PageIndex <= 0)
        {
            filter.PageIndex = 1;
        }

        // Chuẩn hóa PageSize
        if (filter.PageSize <= 0)
        {
            filter.PageSize = DefaultPageSize;
        }

        if (filter.PageSize > MaximumPageSize)
        {
            filter.PageSize = MaximumPageSize;
        }

        var hasKeyword = !string.IsNullOrWhiteSpace(filter.Keyword);

        int totalRecords;
        int totalPages;
        bool hasNextPage = false;

        List<AdminQuestionListItemDto> questionItems;

        if (hasKeyword)
        {
            /*
             * Khi tìm kiếm:
             * Không CountAsync toàn bộ dữ liệu.
             * Lấy dư 1 dòng để kiểm tra có trang tiếp theo hay không.
             */
            questionItems = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.QuestionId)
                .Skip((filter.PageIndex - 1) * filter.PageSize)
                .Take(filter.PageSize + 1)
                .Select(x => new AdminQuestionListItemDto
                {
                    QuestionId = x.QuestionId,
                    CategoryId = x.CategoryId,
                    Category = null,
                    Question = x.QuestionText,
                    Difficulty = x.Difficulty,
                    Position = x.Position != null
                        ? x.Position.Name
                        : null,
                    Status = x.Status,
                    IsDeleted = x.IsDeleted,
                    DeletedAt = x.DeletedAt,
                    DeletedBy = x.DeletedBy,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            hasNextPage = questionItems.Count > filter.PageSize;

            if (hasNextPage)
            {
                // Xóa dòng lấy dư
                questionItems.RemoveAt(questionItems.Count - 1);
            }

            /*
             * Vì không CountAsync nên không biết tổng chính xác.
             * Giá trị này chỉ đại diện cho số kết quả đã đi qua.
             */
            totalRecords =
                ((filter.PageIndex - 1) * filter.PageSize)
                + questionItems.Count
                + (hasNextPage ? 1 : 0);

            totalPages = hasNextPage
                ? filter.PageIndex + 1
                : filter.PageIndex;
        }
        else
        {
            /*
             * Không có keyword:
             * Category, Status, Difficulty thường nhẹ hơn,
             * vẫn giữ CountAsync để phân trang chính xác.
             */
            totalRecords = await query.CountAsync();

            totalPages = totalRecords == 0
                ? 0
                : (int)Math.Ceiling(
                    (double)totalRecords / filter.PageSize
                );

            if (totalPages > 0 &&
                filter.PageIndex > totalPages)
            {
                filter.PageIndex = totalPages;
            }

            questionItems = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.QuestionId)
                .Skip((filter.PageIndex - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(x => new AdminQuestionListItemDto
                {
                    QuestionId = x.QuestionId,
                    CategoryId = x.CategoryId,
                    Category = null,
                    Question = x.QuestionText,
                    Difficulty = x.Difficulty,
                    Position = x.Position != null
                        ? x.Position.Name
                        : null,
                    Status = x.Status,
                    IsDeleted = x.IsDeleted,
                    DeletedAt = x.DeletedAt,
                    DeletedBy = x.DeletedBy,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

        // Lấy danh sách CategoryId xuất hiện trong trang hiện tại
        var categoryIds = questionItems
            .Where(x => x.CategoryId.HasValue)
            .Select(x => x.CategoryId!.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, string> categoryDictionary;

        if (categoryIds.Count == 0)
        {
            categoryDictionary =
                new Dictionary<Guid, string>();
        }
        else
        {
            categoryDictionary = await _db.QuestionCategories
                .AsNoTracking()
                .Where(x =>
                    categoryIds.Contains(x.CategoryId)
                )
                .ToDictionaryAsync(
                    x => x.CategoryId,
                    x => x.Category
                );
        }

        foreach (var item in questionItems)
        {
            if (!item.CategoryId.HasValue)
            {
                continue;
            }

            if (categoryDictionary.TryGetValue(
                item.CategoryId.Value,
                out var categoryName))
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
    //---------------------------------
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
            PositionId = question.PositionId,
            Position = question.Position?.Name,
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

    /// <summary>
    /// Đổi tên position (text từ form câu hỏi) thành entity danh mục Positions:
    /// tìm theo tên (chưa xoá); chỉ có bản đã xoá mềm cùng tên -> khôi phục;
    /// chưa có -> tạo mới (danh mục tự mở rộng — form câu hỏi không phải đổi).
    /// Tên rỗng/trắng -> null (câu hỏi không gắn position).
    /// </summary>
    //private async Task<Position?> ResolvePositionAsync(string? name)
    //{
    //    var normalized = NormalizeNullable(name);
    //    if (normalized is null)
    //    {
    //        return null;
    //    }

    //    var active = await _db.Positions
    //        .FirstOrDefaultAsync(p => p.Name == normalized && !p.IsDeleted);
    //    if (active is not null)
    //    {
    //        return active;
    //    }

    //    var deleted = await _db.Positions
    //        .FirstOrDefaultAsync(p => p.Name == normalized && p.IsDeleted);
    //    if (deleted is not null)
    //    {
    //        deleted.IsDeleted = false;
    //        deleted.DeletedAt = null;
    //        deleted.UpdatedAt = VietnamTime.Now;
    //        return deleted;
    //    }

    //    var now = VietnamTime.Now;
    //    var position = new Position
    //    {
    //        PositionId = Guid.NewGuid(),
    //        Name = normalized,
    //        CreatedAt = now,
    //        UpdatedAt = now
    //    };
    //    await _db.Positions.AddAsync(position);
    //    return position;
    //}

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

    ///thêm 
    ///
    public async Task<List<AdminPositionDto>> GetAllPositionsAsync()
    {
        return await _db.Positions
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .Select(p => new AdminPositionDto
            {
                PositionId = p.PositionId,
                Name = p.Name
            })
            .ToListAsync();
    }
}
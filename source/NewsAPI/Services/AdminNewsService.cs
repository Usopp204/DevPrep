using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NewsAPI.DTOs;
using NewsAPI.Models;
using NewsAPI.Repositories.Interfaces;
using NewsAPI.Services.Interfaces;

namespace NewsAPI.Services;

public class AdminNewsService : IAdminNewsService
{
    private readonly IAdminNewsRepository _repo;
    private readonly NewsDbContext _context;

    public AdminNewsService(IAdminNewsRepository repo, NewsDbContext context)
    {
        _repo = repo;
        _context = context;
    }

    public async Task<PagedResult<AdminNewsListItemDto>> GetListAsync(AdminNewsQueryDto q)
    {
        // Admin list phải lấy cả bài đã xóa mềm để có thể Restore
        var query = _repo.Query();

        query = ApplyFilter(query, q);

        query = query.OrderByDescending(x => x.CreatedAt);

        return await ExecutePaginationAsync(query, q);
    }

    public async Task<PagedResult<AdminNewsListItemDto>> FilterAsync(AdminNewsQueryDto q)
    {
        // Không lọc !IsDeleted mặc định.
        // All Statuses sẽ hiện cả IsDeleted = true và false.
        var query = _repo.Query();

        query = ApplyFilter(query, q);

        query = query.OrderByDescending(x => x.CreatedAt);

        return await ExecutePaginationAsync(query, q);
    }

    private IQueryable<NewsArticle> ApplyFilter(IQueryable<NewsArticle> query, AdminNewsQueryDto q)
    {
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            const string collation = "SQL_Latin1_General_CP1_CI_AI";

            query = query.Where(x =>
                EF.Functions.Collate(x.Title, collation).Contains(EF.Functions.Collate(kw, collation)) ||
                EF.Functions.Collate(x.Slug, collation).Contains(EF.Functions.Collate(kw, collation)) ||
                (x.Summary != null && EF.Functions.Collate(x.Summary, collation).Contains(EF.Functions.Collate(kw, collation))) ||
                (x.Content != null && EF.Functions.Collate(x.Content, collation).Contains(EF.Functions.Collate(kw, collation)))
            );
        }

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            var status = q.Status.Trim();

            if (status.Equals("Deleted", StringComparison.OrdinalIgnoreCase))
            {
                // Bộ lọc Deleted lấy tất cả bài đã xóa mềm,
                // không phụ thuộc Status trong DB đang là Published/Draft/Archived.
                query = query.Where(x => x.IsDeleted);
            }
            else
            {
                // Published, Draft, Archived chỉ lấy bài chưa xóa mềm.
                query = query.Where(x =>
                    !x.IsDeleted &&
                    x.Status.ToLower() == status.ToLower()
                );
            }
        }

        if (q.CategoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == q.CategoryId.Value);
        }

        return query;
    }

    public async Task<AdminNewsDetailDto?> GetDetailAsync(Guid id)
    {
        var article = await _repo.GetByIdAsync(id);
        if (article == null) return null;

        return MapToDetailDto(article);
    }

    public async Task<AdminNewsDetailDto> CreateAsync(AdminCreateNewsDto dto, Guid authorId)
    {
        if (dto.CategoryId.HasValue)
        {
            var categoryExists = await _context.NewsCategories
                .AnyAsync(c => c.CategoryId == dto.CategoryId.Value);

            if (!categoryExists)
            {
                throw new Exception("Provided Category ID does not exist.");
            }
        }

        // Nếu admin để trống Slug -> tự sinh từ Title.
        // Nếu admin có nhập Slug -> vẫn slugify lại (không tin dữ liệu client gửi lên).
        var rawSlugSource = string.IsNullOrWhiteSpace(dto.Slug) ? dto.Title : dto.Slug;
        var finalSlug = GenerateSlug(rawSlugSource);

        if (string.IsNullOrWhiteSpace(finalSlug))
        {
            throw new Exception("Unable to generate a valid slug from the title. Please provide a title with valid characters.");
        }

        // KHÔNG tự động đổi slug khi trùng nữa -> báo lỗi để admin tự sửa.
        await EnsureSlugNotDuplicateAsync(finalSlug);

        var article = new NewsArticle
        {
            NewsId = Guid.NewGuid(),
            CategoryId = dto.CategoryId,
            Title = dto.Title,
            Slug = finalSlug,
            Summary = dto.Summary,
            Content = dto.Content,
            ThumbnailUrl = dto.ThumbnailUrl,
            AuthorId = authorId,
            Status = dto.Status,
            ViewCount = 0,
            IsDeleted = false,
            DeletedAt = null,
            DeletedBy = null,
            CreatedAt = DateTime.UtcNow.AddHours(7),
            UpdatedAt = DateTime.UtcNow.AddHours(7)
        };

        if (dto.Status.Equals("Published", StringComparison.OrdinalIgnoreCase))
        {
            article.PublishedAt = DateTime.UtcNow.AddHours(7);
        }

        await _repo.AddAsync(article);
        await _repo.SaveAsync();

        return MapToDetailDto(article);
    }

    public async Task<AdminNewsDetailDto?> UpdateAsync(Guid id, AdminUpdateNewsDto dto)
    {
        var article = await _repo.GetByIdAsync(id);

        if (article == null || article.IsDeleted)
        {
            return null;
        }

        if (dto.CategoryId.HasValue)
        {
            var categoryExists = await _context.NewsCategories
                .AnyAsync(c => c.CategoryId == dto.CategoryId.Value);

            if (!categoryExists)
            {
                throw new Exception("Provided Category ID does not exist.");
            }
        }

        // Giống Create: để trống Slug -> sinh từ Title; có nhập -> vẫn slugify lại.
        var rawSlugSource = string.IsNullOrWhiteSpace(dto.Slug) ? dto.Title : dto.Slug;
        var finalSlug = GenerateSlug(rawSlugSource);

        if (string.IsNullOrWhiteSpace(finalSlug))
        {
            throw new Exception("Unable to generate a valid slug from the title. Please provide a title with valid characters.");
        }

        // Chỉ báo lỗi nếu slug trùng với MỘT BÀI KHÁC (không tính chính bài đang sửa).
        await EnsureSlugNotDuplicateAsync(finalSlug, excludeId: id);

        article.CategoryId = dto.CategoryId;
        article.Title = dto.Title;
        article.Slug = finalSlug;
        article.Summary = dto.Summary;
        article.Content = dto.Content;
        article.ThumbnailUrl = dto.ThumbnailUrl;

        if (dto.Status.Equals("Published", StringComparison.OrdinalIgnoreCase) &&
            !article.Status.Equals("Published", StringComparison.OrdinalIgnoreCase))
        {
            article.PublishedAt = DateTime.UtcNow.AddHours(7);
        }

        article.Status = dto.Status;
        article.UpdatedAt = DateTime.UtcNow.AddHours(7);

        await _repo.SaveAsync();

        return MapToDetailDto(article);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid adminId)
    {
        var article = await _repo.GetByIdAsync(id);

        if (article == null || article.IsDeleted)
        {
            return false;
        }

        article.IsDeleted = true;
        article.DeletedBy = adminId;
        article.DeletedAt = DateTime.UtcNow.AddHours(7);
        article.UpdatedAt = DateTime.UtcNow.AddHours(7);

        await _repo.SaveAsync();

        return true;
    }

    public async Task<bool> RestoreAsync(Guid id)
    {
        var article = await _repo.GetByIdAsync(id);

        if (article == null || !article.IsDeleted)
        {
            return false;
        }

        article.IsDeleted = false;
        article.Status = "Draft";
        article.DeletedBy = null;
        article.DeletedAt = null;
        article.UpdatedAt = DateTime.UtcNow.AddHours(7);

        await _repo.SaveAsync();

        return true;
    }

    private async Task<PagedResult<AdminNewsListItemDto>> ExecutePaginationAsync(
        IQueryable<NewsArticle> query,
        AdminNewsQueryDto q)
    {
        if (q.PageIndex <= 0) q.PageIndex = 1;
        if (q.PageSize <= 0) q.PageSize = 10;

        var totalItems = await query.CountAsync();

        var totalPages = (int)Math.Ceiling((double)totalItems / q.PageSize);

        var items = await query
            .AsNoTracking()
            .Skip((q.PageIndex - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(x => new AdminNewsListItemDto
            {
                NewsId = x.NewsId,
                CategoryId = x.CategoryId,
                Title = x.Title,
                Slug = x.Slug,
                Summary = x.Summary,
                ThumbnailUrl = x.ThumbnailUrl,
                Status = x.Status,
                ViewCount = x.ViewCount,
                IsDeleted = x.IsDeleted,
                DeletedAt = x.DeletedAt,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<AdminNewsListItemDto>
        {
            Items = items,
            Page = q.PageIndex,
            PageSize = q.PageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    private AdminNewsDetailDto MapToDetailDto(NewsArticle x) => new()
    {
        NewsId = x.NewsId,
        CategoryId = x.CategoryId,
        Title = x.Title,
        Slug = x.Slug,
        Summary = x.Summary,
        Content = x.Content,
        ThumbnailUrl = x.ThumbnailUrl,
        AuthorId = x.AuthorId,
        Status = x.Status,
        ViewCount = x.ViewCount,
        IsDeleted = x.IsDeleted,
        PublishedAt = x.PublishedAt,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };

    public async Task<List<NewsCategory>> GetAllCategoriesAsync()
    {
        return await _context.NewsCategories
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    // =========================================================
    // SLUGIFY TIẾNG VIỆT + BÁO LỖI KHI TRÙNG (KHÔNG TỰ ĐỔI SLUG)
    // =========================================================

    /// <summary>
    /// Sinh slug từ chuỗi bất kỳ (Title hoặc Slug admin nhập tay).
    /// Bỏ dấu tiếng Việt, chữ thường, thay ký tự lạ/khoảng trắng bằng '-',
    /// gộp nhiều '-' liên tiếp, cắt '-' thừa ở đầu/cuối.
    /// Ví dụ: "Xu hướng AI năm 2026!" -> "xu-huong-ai-nam-2026"
    /// </summary>
    private static string GenerateSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var slug = RemoveDiacritics(input.Trim());

        slug = slug.ToLowerInvariant();

        // Loại bỏ mọi ký tự không phải a-z, 0-9, khoảng trắng, hoặc dấu '-'
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");

        // Thay khoảng trắng (1 hoặc nhiều) bằng '-'
        slug = Regex.Replace(slug, @"\s+", "-");

        // Gộp nhiều dấu '-' liên tiếp thành 1
        slug = Regex.Replace(slug, @"-+", "-");

        // Cắt bỏ '-' thừa ở đầu/cuối chuỗi
        slug = slug.Trim('-');

        return slug;
    }

    /// <summary>
    /// Bỏ dấu tiếng Việt: ệ->e, ư->u, ă->a, ơ->o... và xử lý riêng đ/Đ
    /// (vì đ/Đ không tách dấu được qua Unicode NormalizationForm.FormD).
    /// </summary>
    private static string RemoveDiacritics(string text)
    {
        text = text.Replace('đ', 'd').Replace('Đ', 'D');

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);

            // Bỏ qua các dấu (combining marks) tách ra sau khi Normalize FormD
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Kiểm tra slug đã tồn tại chưa. Nếu trùng -> NÉM LỖI để admin tự sửa,
    /// KHÔNG tự động đổi sang slug khác (vd: khong-tu-them-hau-to -1, -2...).
    /// Lưu ý: Slug có unique index ở DB KHÔNG phân biệt IsDeleted,
    /// nên phải kiểm tra trùng trên TOÀN BỘ bảng (kể cả bài đã xóa mềm),
    /// nếu không sẽ bị lỗi vi phạm unique constraint khi SaveChanges.
    /// </summary>
    private async Task EnsureSlugNotDuplicateAsync(string slug, Guid? excludeId = null)
    {
        var isDuplicate = await _context.NewsArticles.AnyAsync(x =>
            x.Slug == slug &&
            (!excludeId.HasValue || x.NewsId != excludeId.Value));

        if (isDuplicate)
        {
            throw new Exception(
                $"Slug \"{slug}\" đã được sử dụng bởi một bài viết khác. Vui lòng nhập slug khác."
            );
        }
    }
}
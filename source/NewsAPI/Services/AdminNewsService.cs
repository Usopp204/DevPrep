using System;
using System.Collections.Generic;
using System.Linq;
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

        var inputSlug = dto.Slug.ToLower().Trim();

        var isSlugDuplicate = await _context.NewsArticles
            .AnyAsync(x => x.Slug == inputSlug && !x.IsDeleted);

        if (isSlugDuplicate)
        {
            throw new Exception("An article with this Title/Slug already exists. Please modify your title or slug.");
        }

        var article = new NewsArticle
        {
            NewsId = Guid.NewGuid(),
            CategoryId = dto.CategoryId,
            Title = dto.Title,
            Slug = inputSlug,
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

        var inputSlug = dto.Slug.ToLower().Trim();

        var isSlugDuplicate = await _context.NewsArticles
            .AnyAsync(x => x.Slug == inputSlug && x.NewsId != id && !x.IsDeleted);

        if (isSlugDuplicate)
        {
            throw new Exception("This Slug is already taken by another article. Please choose a different title or slug.");
        }

        article.CategoryId = dto.CategoryId;
        article.Title = dto.Title;
        article.Slug = inputSlug;
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
}
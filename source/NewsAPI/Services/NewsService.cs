using Microsoft.EntityFrameworkCore;
using NewsAPI.DTOs;
using NewsAPI.Models;
using NewsAPI.Repositories.Interfaces;
using NewsAPI.Services.Interfaces;

namespace NewsAPI.Services;

public class NewsService : INewsService
{
    private const int MaxPageSize = 50;

    private readonly INewsRepository _repository;

    public NewsService(INewsRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<NewsListItemDto>> GetListAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var skip = (page - 1) * pageSize;

        var query = _repository.QueryPublished();
        var total = await query.CountAsync();

        // Mới nhất trước: ưu tiên PublishedAt, thiếu thì dùng CreatedAt.
        var items = await query
            .OrderByDescending(n => n.PublishedAt ?? n.CreatedAt)
            .Skip(skip).Take(pageSize)
            .Select(n => new NewsListItemDto
            {
                NewsId = n.NewsId,
                Title = n.Title,
                Slug = n.Slug,
                Summary = n.Summary,
                ThumbnailUrl = n.ThumbnailUrl,
                CategoryId = n.CategoryId,
                CategoryName = n.Category != null ? n.Category.Name : null,
                ViewCount = n.ViewCount,
                PublishedAt = n.PublishedAt,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<NewsListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<NewsDetailDto?> GetDetailAsync(Guid id)
    {
        var article = await _repository.GetPublishedByIdAsync(id, tracking: true);
        return article is null ? null : await BuildDetailAsync(article);
    }

    // Xem chi tiết theo slug (URL SEO); null nếu không tồn tại/chưa xuất bản.
    public async Task<NewsDetailDto?> GetDetailBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var article = await _repository.GetPublishedBySlugAsync(slug, tracking: true);
        return article is null ? null : await BuildDetailAsync(article);
    }

    // Tăng lượt xem + map sang DTO — dùng chung cho tra theo id lẫn theo slug.
    private async Task<NewsDetailDto> BuildDetailAsync(NewsArticle article)
    {
        article.ViewCount += 1;
        await _repository.SaveChangesAsync();

        return new NewsDetailDto
        {
            NewsId = article.NewsId,
            Title = article.Title,
            Slug = article.Slug,
            Summary = article.Summary,
            Content = article.Content,
            ThumbnailUrl = article.ThumbnailUrl,
            CategoryId = article.CategoryId,
            CategoryName = article.Category?.Name,
            AuthorId = article.AuthorId,
            ViewCount = article.ViewCount,
            PublishedAt = article.PublishedAt,
            CreatedAt = article.CreatedAt,
            UpdatedAt = article.UpdatedAt
        };
    }
}

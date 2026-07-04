using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsAPI.DTOs;
using NewsAPI.Models;

namespace NewsAPI.Controllers;

[ApiController]
[Route("api/admin/manager/news")]
[Authorize(Roles = "Admin,Manager")]
public class ManagerNewsController : ControllerBase
{
    private const int MaxPageSize = 100;
    private readonly NewsDbContext _db;

    public ManagerNewsController(NewsDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ManagerNewsListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.NewsArticles
            .AsNoTracking()
            .Include(n => n.Category)
            .Where(n => !n.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(n =>
                n.Title.Contains(term) ||
                n.Slug.Contains(term) ||
                (n.Summary != null && n.Summary.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var requestedStatus = status.Trim();
            query = query.Where(n => n.Status == requestedStatus);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(n => n.PublishedAt ?? n.UpdatedAt)
            .ThenByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new ManagerNewsListItemDto
            {
                NewsId = n.NewsId,
                Title = n.Title,
                Slug = n.Slug,
                Summary = n.Summary,
                ThumbnailUrl = n.ThumbnailUrl,
                CategoryName = n.Category != null ? n.Category.Name : null,
                Status = n.Status,
                ViewCount = n.ViewCount,
                PublishedAt = n.PublishedAt,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt
            })
            .ToListAsync();

        var result = new PagedResult<ManagerNewsListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };

        return Ok(ApiResponse<PagedResult<ManagerNewsListItemDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ManagerNewsDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var item = await _db.NewsArticles
            .AsNoTracking()
            .Include(n => n.Category)
            .Where(n => n.NewsId == id && !n.IsDeleted)
            .Select(n => new ManagerNewsDetailDto
            {
                NewsId = n.NewsId,
                Title = n.Title,
                Slug = n.Slug,
                Summary = n.Summary,
                Content = n.Content,
                ThumbnailUrl = n.ThumbnailUrl,
                CategoryName = n.Category != null ? n.Category.Name : null,
                AuthorId = n.AuthorId,
                Status = n.Status,
                ViewCount = n.ViewCount,
                PublishedAt = n.PublishedAt,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (item is null)
        {
            return NotFound(ApiResponse<object>.Fail("News article not found."));
        }

        return Ok(ApiResponse<ManagerNewsDetailDto>.Ok(item));
    }
}

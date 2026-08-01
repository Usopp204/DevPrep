using Microsoft.AspNetCore.Mvc;
using NewsAPI.DTOs;
using NewsAPI.Services.Interfaces;

namespace NewsAPI.Controllers;

/// <summary>
/// Tin tức công khai cho guest và candidate (không cần đăng nhập).
/// </summary>
[ApiController]
[Route("api/news")]
public class NewsController : ControllerBase
{
    private readonly INewsService _service;

    public NewsController(INewsService service)
    {
        _service = service;
    }

    /// <summary>View/Search/Filter News: danh sách tin đã xuất bản, lọc theo từ khoá và/hoặc danh mục (tuỳ chọn), sắp xếp mới nhất tới cũ, có phân trang.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NewsListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList([FromQuery] NewsQueryDto query)
    {
        var result = await _service.GetListAsync(query);
        return Ok(ApiResponse<PagedResult<NewsListItemDto>>.Ok(result));
    }

    /// <summary>Danh mục đang có ít nhất một bài đã xuất bản — dùng để dựng chip lọc trên trang News.</summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<List<NewsCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _service.GetCategoriesAsync();
        return Ok(ApiResponse<List<NewsCategoryDto>>.Ok(categories));
    }

    /// <summary>View News Detail theo id (giữ tương thích link cũ) + tăng lượt xem.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<NewsDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var detail = await _service.GetDetailAsync(id);
        if (detail is null) return NotFound(ApiResponse<object>.Fail("News article not found."));
        return Ok(ApiResponse<NewsDetailDto>.Ok(detail));
    }

    /// <summary>View News Detail theo slug (URL SEO) + tăng lượt xem.</summary>
    [HttpGet("slug/{slug}")]
    [ProducesResponseType(typeof(ApiResponse<NewsDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetailBySlug(string slug)
    {
        var detail = await _service.GetDetailBySlugAsync(slug);
        if (detail is null) return NotFound(ApiResponse<object>.Fail("News article not found."));
        return Ok(ApiResponse<NewsDetailDto>.Ok(detail));
    }
}

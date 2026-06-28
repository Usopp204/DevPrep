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

    /// <summary>View News: danh sách tin đã xuất bản, sắp xếp mới nhất tới cũ, có phân trang.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NewsListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _service.GetListAsync(page, pageSize);
        return Ok(ApiResponse<PagedResult<NewsListItemDto>>.Ok(result));
    }

    /// <summary>View News Detail: chi tiết một bài tin + tăng lượt xem.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<NewsDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var detail = await _service.GetDetailAsync(id);
        if (detail is null) return NotFound(ApiResponse<object>.Fail("News article not found."));
        return Ok(ApiResponse<NewsDetailDto>.Ok(detail));
    }
}

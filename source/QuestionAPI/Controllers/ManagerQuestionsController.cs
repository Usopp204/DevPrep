using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestionAPI.DTOs;
using QuestionAPI.Services;

namespace QuestionAPI.Controllers;

[ApiController]
[Route("api/admin/manager/questions")]
[Authorize(Roles = "Manager,Admin")]
public class ManagerQuestionsController : ControllerBase
{
    private readonly IQuestionService _service;

    public ManagerQuestionsController(IQuestionService service)
    {
        _service = service;
    }

    /// <summary>
    /// View Question Bank List (Manager) - read-only
    /// GET /api/admin/manager/questions
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<QuestionListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] string? questionType = null,
        [FromQuery] string? level = null,
        [FromQuery] string? difficulty = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? categoryId = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var result = await _service.GetListAsync(page, pageSize, keyword, questionType, level, difficulty, status, categoryId);
        return Ok(ApiResponse<PagedResult<QuestionListItemDto>>.Ok(result));
    }

    /// <summary>
    /// View Question Details (Manager) - read-only with reference answer
    /// GET /api/admin/manager/questions/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<QuestionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var item = await _service.GetByIdAsync(id);
        if (item is null)
            return NotFound(ApiResponse<QuestionDetailDto>.Fail($"Question with id '{id}' not found."));

        return Ok(ApiResponse<QuestionDetailDto>.Ok(item));
    }

    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<List<QuestionCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _service.GetCategoriesAsync();
        return Ok(ApiResponse<List<QuestionCategoryDto>>.Ok(categories));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestionAPI.DTOs;
using QuestionAPI.Services.Interfaces;
using System.Security.Claims;

namespace QuestionAPI.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminQuestionController : ControllerBase
{
    private readonly IAdminQuestionService _service;

    public AdminQuestionController(IAdminQuestionService service)
    {
        _service = service;
    }

    // GET /api/admin/questions
    [HttpGet("questions")]
    public async Task<IActionResult> Get([FromQuery] AdminQuestionQueryDto q)
    {
        var result = await _service.GetAsync(q);

        return Ok(ApiResponse<AdminPagedResultDto<AdminQuestionListItemDto>>.Ok(
            result,
            "Fetched questions successfully."
        ));
    }

    // GET /api/admin/questions/{id}
    [HttpGet("questions/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var result = await _service.GetDetailAsync(id);

        if (result == null)
        {
            return NotFound(ApiResponse<object>.Fail("Question not found."));
        }

        return Ok(ApiResponse<AdminQuestionDetailDto>.Ok(
            result,
            "Fetched question detail successfully."
        ));
    }

    // POST /api/admin/questions
    [HttpPost("questions")]
    public async Task<IActionResult> Create(AdminCreateQuestionDto dto)
    {
        try
        {
            var adminId = GetAdminId();
            var result = await _service.CreateAsync(dto, adminId);

            return Ok(ApiResponse<AdminQuestionDetailDto>.Ok(
                result,
                "Created question successfully và synced sang AI."
            ));
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(ApiResponse<object>.Fail(message));
        }
    }

    // PUT /api/admin/questions/{id}
    [HttpPut("questions/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, AdminUpdateQuestionDto dto)
    {
        try
        {
            var adminId = GetAdminId();
            var result = await _service.UpdateAsync(id, dto, adminId);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.Fail("Question not found to update."));
            }

            return Ok(ApiResponse<AdminQuestionDetailDto>.Ok(
                result,
                "Updated question successfully và synced sang AI."
            ));
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(ApiResponse<object>.Fail(message));
        }
    }

    // DELETE /api/admin/questions/{id}
    [HttpDelete("questions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var adminId = GetAdminId();
            var success = await _service.DeleteAsync(id, adminId);

            if (!success)
            {
                return NotFound(ApiResponse<object>.Fail(
                    "Question not found or already soft-deleted."
                ));
            }

            return Ok(ApiResponse<object>.Ok(
                true,
                "Soft-deleted question successfully và removed khỏi AI."
            ));
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(ApiResponse<object>.Fail(message));
        }
    }

    // POST /api/admin/questions/{id}/restore
    [HttpPost("questions/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id)
    {
        try
        {
            var success = await _service.RestoreAsync(id);

            if (!success)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    "Failed to restore. Question does not exist or is not soft-deleted."
                ));
            }

            return Ok(ApiResponse<object>.Ok(
                true,
                "Restored question successfully và synced sang AI."
            ));
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(ApiResponse<object>.Fail(message));
        }
    }

    // POST /api/admin/questions/reindex
    // Endpoint đồng bộ lại toàn bộ SQL Active sang ChromaDB
    [HttpPost("questions/reindex")]
    public async Task<IActionResult> ReindexAll()
    {
        try
        {
            var result = await _service.ReindexAllQuestionsAsync();

            return Ok(ApiResponse<AdminReindexResultDto>.Ok(
                result,
                "Re-indexed active questions to AI Service successfully."
            ));
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(ApiResponse<object>.Fail(message));
        }
    }

    // GET /api/admin/search-questions
    [HttpGet("search-questions")]
    public async Task<IActionResult> Search([FromQuery] AdminQuestionQueryDto q)
    {
        var result = await _service.SearchAsync(q);

        return Ok(ApiResponse<AdminPagedResultDto<AdminQuestionListItemDto>>.Ok(
            result,
            "Searched questions successfully."
        ));
    }

    // GET /api/admin/filter-questions
    [HttpGet("filter-questions")]
    public async Task<IActionResult> Filter([FromQuery] AdminQuestionQueryDto q)
    {
        var result = await _service.FilterAsync(q);

        return Ok(ApiResponse<AdminPagedResultDto<AdminQuestionListItemDto>>.Ok(
            result,
            "Filtered questions successfully."
        ));
    }

    // GET /api/admin/questions/categories
    [HttpGet("questions/categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _service.GetAllCategoriesAsync();

        return Ok(ApiResponse<List<AdminCategoryDto>>.Ok(
            categories,
            "Fetched question categories successfully."
        ));
    }

    private Guid? GetAdminId()
    {
        var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
              ?? User.FindFirst("sub")?.Value
              ?? User.FindFirst("accountId")?.Value;

        return Guid.TryParse(id, out var parsedGuid)
            ? parsedGuid
            : null;
    }

    //them 
    // GET /api/admin/questions/positions
    [HttpGet("questions/positions")]
    public async Task<IActionResult> GetPositions()
    {
        var positions = await _service.GetAllPositionsAsync();

        return Ok(ApiResponse<List<AdminPositionDto>>.Ok(
            positions,
            "Fetched question positions successfully."
        ));
    }
}
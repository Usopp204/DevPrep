using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestionAPI.DTOs;
using QuestionAPI.DTOs.Positions;
using QuestionAPI.Exceptions;
using QuestionAPI.Services.Interfaces;

namespace QuestionAPI.Controllers;

[ApiController]
[Route("api/admin/positions")]
[Authorize(Roles = "Admin")]
public class AdminPositionsController : ControllerBase
{
    private readonly IPositionService _service;

    public AdminPositionsController(IPositionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] PositionQueryDto query, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.GetPageAsync(query, cancellationToken);
            return Ok(ApiResponse<PositionPagedResultDto>.Ok(result));
        }
        catch (PositionValidationException exception)
        {
            return BadRequest(ApiResponse<object>.Fail(exception.Message));
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result is null)
        {
            return NotFound(ApiResponse<object>.Fail("Position not found."));
        }

        return Ok(ApiResponse<PositionDto>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePositionDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(
                nameof(GetById),
                new { id = result.PositionId },
                ApiResponse<PositionDto>.Ok(result));
        }
        catch (PositionValidationException exception)
        {
            return BadRequest(ApiResponse<object>.Fail(exception.Message));
        }
        catch (PositionDuplicateException exception)
        {
            return Conflict(ApiResponse<object>.Fail(exception.Message));
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePositionDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request, cancellationToken);

            if (result is null)
            {
                return NotFound(ApiResponse<object>.Fail("Position not found."));
            }

            return Ok(ApiResponse<PositionDto>.Ok(result));
        }
        catch (PositionValidationException exception)
        {
            return BadRequest(ApiResponse<object>.Fail(exception.Message));
        }
        catch (PositionDuplicateException exception)
        {
            return Conflict(ApiResponse<object>.Fail(exception.Message));
        }
    }

    /// <summary>Xoá mềm. Câu hỏi đang gắn position vẫn giữ FK và hiển thị tên bình thường.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var found = await _service.SoftDeleteAsync(id, cancellationToken);

        if (!found)
        {
            return NotFound(ApiResponse<object>.Fail("Position not found."));
        }

        return NoContent();
    }

    /// <summary>Khôi phục position đã xoá mềm.</summary>
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.RestoreAsync(id, cancellationToken);

            if (result is null)
            {
                return NotFound(ApiResponse<object>.Fail("Position not found."));
            }

            return Ok(ApiResponse<PositionDto>.Ok(result));
        }
        catch (PositionDuplicateException exception)
        {
            return Conflict(ApiResponse<object>.Fail(exception.Message));
        }
    }
}

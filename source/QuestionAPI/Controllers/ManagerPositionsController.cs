using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestionAPI.DTOs;
using QuestionAPI.DTOs.Positions;
using QuestionAPI.Exceptions;
using QuestionAPI.Services.Interfaces;

namespace QuestionAPI.Controllers;

/// <summary>Manager chỉ được XEM danh sách Position (giống pattern ManagerTechStacksController).</summary>
[ApiController]
[Route("api/admin/manager/positions")]
[Authorize(Roles = "Manager,Admin")]
public class ManagerPositionsController : ControllerBase
{
    private readonly IPositionService _service;

    public ManagerPositionsController(IPositionService service)
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
}

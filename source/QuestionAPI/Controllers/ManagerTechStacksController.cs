using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestionAPI.DTOs;
using QuestionAPI.DTOs.TechStacks;
using QuestionAPI.Exceptions;
using QuestionAPI.Services.Interfaces;

namespace QuestionAPI.Controllers;

[ApiController]
[Route("api/admin/manager/tech-stacks")]
[Authorize(Roles = "Manager,Admin")]
public class ManagerTechStacksController : ControllerBase
{
    private readonly ITechStackService _service;

    public ManagerTechStacksController(ITechStackService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] TechStackQueryDto query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.GetPageAsync(query, cancellationToken);
            return Ok(ApiResponse<TechStackPagedResultDto>.Ok(result));
        }
        catch (TechStackValidationException exception)
        {
            return BadRequest(ApiResponse<object>.Fail(exception.Message));
        }
    }
}

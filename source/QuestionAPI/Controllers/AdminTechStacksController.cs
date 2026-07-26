using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestionAPI.DTOs;
using QuestionAPI.DTOs.TechStacks;
using QuestionAPI.Exceptions;
using QuestionAPI.Services.Interfaces;

namespace QuestionAPI.Controllers;

[ApiController]
[Route("api/admin/tech-stacks")]
[Authorize(Roles = "Admin")]
public class AdminTechStacksController : ControllerBase
{
    private readonly ITechStackService _service;

    public AdminTechStacksController(ITechStackService service)
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

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result is null)
        {
            return NotFound(ApiResponse<object>.Fail("Tech Stack not found."));
        }

        return Ok(ApiResponse<TechStackDto>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTechStackDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(
                nameof(GetById),
                new { id = result.TechStackId },
                ApiResponse<TechStackDto>.Ok(result));
        }
        catch (TechStackValidationException exception)
        {
            return BadRequest(ApiResponse<object>.Fail(exception.Message));
        }
        catch (TechStackDuplicateException exception)
        {
            return Conflict(ApiResponse<object>.Fail(exception.Message));
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTechStackDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request, cancellationToken);

            if (result is null)
            {
                return NotFound(ApiResponse<object>.Fail("Tech Stack not found."));
            }

            return Ok(ApiResponse<TechStackDto>.Ok(result));
        }
        catch (TechStackValidationException exception)
        {
            return BadRequest(ApiResponse<object>.Fail(exception.Message));
        }
        catch (TechStackDuplicateException exception)
        {
            return Conflict(ApiResponse<object>.Fail(exception.Message));
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var found = await _service.DeactivateAsync(id, cancellationToken);

        if (!found)
        {
            return NotFound(ApiResponse<object>.Fail("Tech Stack not found."));
        }

        return NoContent();
    }
}

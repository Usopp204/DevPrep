using InterviewAPI.DTOs;
using InterviewAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewAPI.Controllers;

[ApiController]
[Route("api/admin/manager/sessions")]
[Authorize(Roles = "Manager")]
public class ManagerInterviewSessionsController : ControllerBase
{
    private readonly IInterviewService _service;

    public ManagerInterviewSessionsController(IInterviewService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] InterviewQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetManagerSessionListAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<ManagerInterviewSessionListItemDto>>.Ok(result));
    }

    [HttpGet("{id:guid}/log")]
    public async Task<IActionResult> GetLog(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _service.GetManagerSessionLogAsync(id, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<object>.Fail("Interview session not found."));
        }

        return Ok(ApiResponse<ManagerInterviewSessionLogDto>.Ok(detail));
    }
}

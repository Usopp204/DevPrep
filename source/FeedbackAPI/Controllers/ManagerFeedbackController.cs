using FeedbackAPI.DTOs;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeedbackAPI.Controllers;

/// <summary>
/// Quản lý System Feedback cho Manager.
/// </summary>
[ApiController]
[Route("api/admin/manager/feedbacks")]
[Authorize(Roles = "Admin,Manager")]
public class ManagerFeedbackController : ControllerBase
{
    private readonly ISystemFeedbackService _service;

    public ManagerFeedbackController(ISystemFeedbackService service)
    {
        _service = service;
    }

    /// <summary>View Feedback List - Read-only access to a paginated overview of all user feedback metrics to monitor system service quality.</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] SystemFeedbackQuery query)
    {
        var result = await _service.GetListAsync(query);
        return Ok(ApiResponse<PagedResult<SystemFeedbackListItemDto>>.Ok(result));
    }

    /// <summary>View Feedback Details - Read full textual contents and system scores of specific user complaints or suggestions.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var result = await _service.GetDetailAsync(id);
        if (result is null) return NotFound(ApiResponse<object>.Fail("Feedback not found."));
        return Ok(ApiResponse<SystemFeedbackListItemDto>.Ok(result));
    }
}

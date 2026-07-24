using FeedbackAPI.DTOs;
using FeedbackAPI.Services;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeedbackAPI.Controllers;

[ApiController]
[Route("api/admin/manager/feedback/reports")]
[Authorize(Roles = "Manager")]
public sealed class ManagerFeedbackReportsController(IFeedbackReportService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] FeedbackReportQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.GetListAsync(query, cancellationToken);
            return Ok(ApiResponse<PagedResult<FeedbackReportListItemDto>>.Ok(result));
        }
        catch (FeedbackReportValidationException exception)
        {
            return BadRequest(ApiResponse<object>.Fail(exception.Message));
        }
    }

    [HttpGet("{reportId:guid}")]
    public async Task<IActionResult> GetDetails(Guid reportId, CancellationToken cancellationToken)
    {
        var result = await service.GetDetailsAsync(reportId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<object>.Fail("Feedback report was not found."))
            : Ok(ApiResponse<FeedbackReportDetailsDto>.Ok(result));
    }
}

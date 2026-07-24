using System.Security.Claims;
using FeedbackAPI.DTOs;
using FeedbackAPI.Services;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeedbackAPI.Controllers;

[ApiController]
[Route("api/admin/feedback/reports")]
[Authorize(Roles = "Admin")]
public sealed class AdminFeedbackReportsController(IFeedbackReportService service) : ControllerBase
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

    [HttpPatch("{reportId:guid}/dismiss")]
    public Task<IActionResult> Dismiss(
        Guid reportId,
        [FromBody] ResolveFeedbackReportRequest request,
        CancellationToken cancellationToken) =>
        ResolveAsync(
            (adminId, ct) => service.DismissAsync(reportId, adminId, request, ct),
            cancellationToken);

    [HttpPatch("{reportId:guid}/delete-feedback")]
    public Task<IActionResult> DeleteFeedback(
        Guid reportId,
        [FromBody] ResolveFeedbackReportRequest request,
        CancellationToken cancellationToken) =>
        ResolveAsync(
            (adminId, ct) => service.DeleteFeedbackAsync(reportId, adminId, request, ct),
            cancellationToken);

    private async Task<IActionResult> ResolveAsync(
        Func<Guid, CancellationToken, Task<FeedbackReportResolutionDto>> action,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var adminId))
        {
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));
        }

        try
        {
            var result = await action(adminId, cancellationToken);
            return Ok(ApiResponse<FeedbackReportResolutionDto>.Ok(result));
        }
        catch (FeedbackReportValidationException exception)
        {
            return BadRequest(ApiResponse<object>.Fail(exception.Message));
        }
        catch (FeedbackReportForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(exception.Message));
        }
        catch (FeedbackReportNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Fail(exception.Message));
        }
        catch (FeedbackReportConflictException exception)
        {
            return Conflict(ApiResponse<object>.Fail(exception.Message));
        }
    }

    private bool TryGetAccountId(out Guid accountId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out accountId);
}

using FeedbackAPI.DTOs;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeedbackAPI.Controllers;

[ApiController]
[Route("api/admin/feedbacks/statistics")]
[Authorize(Roles = "Admin,Manager")]
public sealed class AdminFeedbackStatisticsController(
    IFeedbackStatisticsService service) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardStatistics(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (!from.HasValue || !to.HasValue || from.Value > to.Value)
            return BadRequest(ApiResponse<object>.Fail("The From date must be on or before the To date."));

        var inclusiveDays = to.Value.DayNumber - from.Value.DayNumber + 1;
        if (inclusiveDays > 366)
            return BadRequest(ApiResponse<object>.Fail("The selected date range must contain no more than 366 days."));

        var result = await service.GetDashboardStatisticsAsync(
            from.Value,
            to.Value,
            cancellationToken);
        return Ok(ApiResponse<FeedbackDashboardStatisticsDto>.Ok(result));
    }
}
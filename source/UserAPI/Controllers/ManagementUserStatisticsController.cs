using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserAPI.DTOs;
using UserAPI.Services.Interfaces;

namespace UserAPI.Controllers;

[ApiController]
[Route("api/admin/users/statistics/candidates")]
[Authorize(Roles = "Admin,Manager")]
public sealed class ManagementUserStatisticsController(IAdminUserService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CandidateStatisticsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCandidateStatistics(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (!from.HasValue || !to.HasValue || from.Value > to.Value)
            return BadRequest(ApiResponse<object>.Fail("The From date must be on or before the To date."));

        var inclusiveDays = to.Value.DayNumber - from.Value.DayNumber + 1;
        if (inclusiveDays > 366)
            return BadRequest(ApiResponse<object>.Fail("The selected date range must contain no more than 366 days."));

        try
        {
            var data = await service.GetCandidateStatisticsAsync(
                from.Value,
                to.Value,
                Request.Headers.Authorization.ToString(),
                cancellationToken);
            return Ok(ApiResponse<CandidateStatisticsDto>.Ok(data));
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                ApiResponse<object>.Fail("Candidate statistics are temporarily unavailable."));
        }
    }
}

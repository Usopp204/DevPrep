using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StatisticsAPI.DTOs;
using StatisticsAPI.Helpers;
using StatisticsAPI.Services;

namespace StatisticsAPI.Controllers;

[ApiController]
[Route("api/admin/statistics")]
[Authorize(Roles = "Admin,Manager")]
public class AdminStatisticsController(
    IStatisticsQueryService queryService,
    IStatisticsCollector collector) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(VietnamTime.Today);
        if (!DashboardDateRange.TryResolve(from, to, today, out var range, out var error))
            return BadRequest(ApiResponse<object>.Fail(error!));

        var data = await queryService.GetDashboardAsync(range.From, range.To, cancellationToken);
        return Ok(ApiResponse<ManagementDashboardDto>.Ok(data));
    }

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
    {
        var data = await queryService.GetOverviewAsync(cancellationToken);
        return Ok(ApiResponse<StatisticsOverviewDto>.Ok(data));
    }

    [HttpGet("timeseries")]
    public async Task<IActionResult> TimeSeries(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var toDate = to ?? DateOnly.FromDateTime(VietnamTime.Today);
        var fromDate = from ?? toDate.AddDays(-29);
        var data = await queryService.GetTimeSeriesAsync(fromDate, toDate, cancellationToken);
        return Ok(ApiResponse<TimeSeriesResult>.Ok(data));
    }

    [HttpPost("rebuild")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Rebuild(CancellationToken cancellationToken)
    {
        var result = await collector.RunAsync(waitForAvailability: false, cancellationToken);
        if (result.Busy)
        {
            return Conflict(new ApiResponse<RebuildResultDto>
            {
                Success = false,
                Message = "A statistics collection is already in progress.",
                Data = result
            });
        }

        if (!result.Ok)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiResponse<RebuildResultDto>
            {
                Success = false,
                Message = "Dashboard statistics could not be refreshed. The previous generation was preserved.",
                Data = result
            });
        }

        return Ok(ApiResponse<RebuildResultDto>.Ok(result, "Dashboard statistics rebuilt successfully."));
    }
}
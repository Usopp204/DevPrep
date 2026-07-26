using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StatisticsAPI.DTOs;
using StatisticsAPI.Helpers;
using StatisticsAPI.Services;

namespace StatisticsAPI.Controllers;

/// <summary>
/// Đọc read-model thống kê cho Admin Dashboard. Đọc thẳng DevPrep_Statistics (KHÔNG fan-out sang service khác).
/// Route full-path (gateway đã bỏ PathRemovePrefix cho /api/admin/statistics).
/// </summary>
[ApiController]
[Route("api/admin/statistics")]
[Authorize(Roles = "Admin,Manager")]
public class AdminStatisticsController(
    IStatisticsQueryService queryService,
    IStatisticsCollector collector) : ControllerBase
{
    /// <summary>Các con số tổng hiện tại (stat card).</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        var data = await queryService.GetOverviewAsync(ct);
        return Ok(ApiResponse<StatisticsOverviewDto>.Ok(data));
    }

    /// <summary>Chuỗi số liệu theo ngày trong [from..to] (yyyy-MM-dd) — vẽ chart. Mặc định 30 ngày gần nhất.</summary>
    [HttpGet("timeseries")]
    public async Task<IActionResult> TimeSeries([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var toD = to ?? DateOnly.FromDateTime(VietnamTime.Today);
        var fromD = from ?? toD.AddDays(-29);
        var data = await queryService.GetTimeSeriesAsync(fromD, toD, ct);
        return Ok(ApiResponse<TimeSeriesResult>.Ok(data));
    }

    /// <summary>Tổng hợp lại ngay (kéo số liệu từ các service). Chỉ Admin. Cần các service kia đang chạy.</summary>
    [HttpPost("rebuild")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Rebuild(CancellationToken ct)
    {
        var result = await collector.RunAsync(ct);
        return Ok(ApiResponse<RebuildResultDto>.Ok(result,
            result.Warnings.Count == 0 ? "Rebuilt OK." : $"Rebuilt with {result.Warnings.Count} warning(s)."));
    }
}

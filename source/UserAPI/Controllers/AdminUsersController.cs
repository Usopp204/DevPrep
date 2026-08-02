using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserAPI.DTOs;
using UserAPI.Services.Interfaces;

namespace UserAPI.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _service;

    public AdminUsersController(IAdminUserService service)
    {
        _service = service;
    }

    [HttpGet("statistics/candidates")]
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
            var data = await _service.GetCandidateStatisticsAsync(
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
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminUserListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers([FromQuery] AdminUserQueryDto query)
    {
        var result = await _service.GetUsersAsync(query);

        return Ok(ApiResponse<PagedResult<AdminUserListItemDto>>.Ok(
            result,
            "Fetched admin users successfully."
        ));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var result = await _service.GetUserDetailAsync(id);

        if (result == null)
        {
            return NotFound(ApiResponse<object>.Fail("User not found."));
        }

        return Ok(ApiResponse<AdminUserDetailDto>.Ok(
            result,
            "Fetched admin user detail successfully."
        ));
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] AdminUpdateUserStatusDto dto)
    {
        try
        {
            var adminAccountId = GetCurrentAccountId();

            var success = await _service.UpdateStatusAsync(id, dto, adminAccountId);

            if (!success)
            {
                return NotFound(ApiResponse<object>.Fail("User not found."));
            }

            return Ok(ApiResponse<object>.Ok(
                true,
                "Updated user status successfully."
            ));
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(ApiResponse<object>.Fail(message));
        }
    }
    [HttpPatch("{id:guid}/role")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] AdminUpdateUserRoleDto dto)
    {
        try
        {
            // Lấy AccountId của admin đang đăng nhập hiện tại
            var adminAccountId = GetCurrentAccountId();

            //Truyền thêm adminAccountId làm tham số thứ 3 vào hàm UpdateRoleAsync
            var success = await _service.UpdateRoleAsync(id, dto, adminAccountId);

            if (!success)
            {
                return NotFound(ApiResponse<object>.Fail("User not found."));
            }

            return Ok(ApiResponse<object>.Ok(
                true,
                "Updated user role successfully."
            ));
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(ApiResponse<object>.Fail(message));
        }
    }

    private Guid? GetCurrentAccountId()
    {
        var id =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("accountId");

        return Guid.TryParse(id, out var parsed)
            ? parsed
            : null;
    }

    [HttpPost("auto-block-inactive")]
    [ProducesResponseType(typeof(ApiResponse<AdminAutoBlockInactiveResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AutoBlockInactiveUsers([FromQuery] int days = 30)
    {
        var result = await _service.AutoBlockInactiveUsersAsync(days);

        return Ok(ApiResponse<AdminAutoBlockInactiveResultDto>.Ok(
            result,
            $"Auto-blocked {result.BlockedCount} inactive users."
        ));
    }
}
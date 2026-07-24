using System.Security.Claims;
using FeedbackAPI.DTOs;
using FeedbackAPI.Services;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeedbackAPI.Controllers;

[ApiController]
[Route("api/admin/feedback")]
[Authorize(Roles = "Admin")]
public class AdminFeedbackModerationController : ControllerBase
{
    private readonly ISystemFeedbackService _systemService;

    public AdminFeedbackModerationController(ISystemFeedbackService systemService)
    {
        _systemService = systemService;
    }

    [HttpPatch("system/{feedbackId:guid}/moderation")]
    public async Task<IActionResult> ModerateSystem(Guid feedbackId, [FromBody] ModerationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out var adminId)) return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));
        try
        {
            var result = await _systemService.ModerateAsync(feedbackId, adminId, request, cancellationToken);
            return result is null
                ? NotFound(ApiResponse<object>.Fail("System feedback not found."))
                : Ok(ApiResponse<ModerationResponse>.Ok(result));
        }
        catch (FeedbackValidationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private bool TryGetAdminId(out Guid adminId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out adminId);
    }
}

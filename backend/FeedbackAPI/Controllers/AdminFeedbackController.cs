using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FeedbackAPI.DTOs;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeedbackAPI.Controllers;

/// <summary>
/// Quản lý feedback cho Admin/Manager.
/// Đọc (list/detail): Admin + Manager. Ghi (update/delete/reply): Admin.
/// {type} trong route nhận "system" hoặc "ai".
/// </summary>
[ApiController]
[Route("api/admin/feedbacks")]
[Authorize(Roles = "Admin,Manager")]
public class AdminFeedbackController : ControllerBase
{
    private readonly IFeedbackService _service;

    public AdminFeedbackController(IFeedbackService service)
    {
        _service = service;
    }

    /// <summary>
    /// Danh sách feedback. type = "AI" -> chỉ feedback AI; "System" -> chỉ feedback hệ thống;
    /// bỏ trống -> tất cả. Luôn sắp xếp mới nhất tới cũ.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] string? type, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!TryParseTypeFilter(type, out var filter))
        {
            return BadRequest(ApiResponse<object>.Fail("Invalid 'type'. Use 'AI', 'System', or leave empty for all."));
        }

        var result = await _service.GetListAsync(filter, page, pageSize);
        return Ok(ApiResponse<PagedResult<FeedbackListItemDto>>.Ok(result));
    }

    /// <summary>Chi tiết một feedback (kèm replies nếu là System).</summary>
    [HttpGet("{type}/{id:guid}")]
    public async Task<IActionResult> GetDetail(string type, Guid id)
    {
        if (!TryParseType(type, out var feedbackType))
        {
            return BadRequest(ApiResponse<object>.Fail("Invalid 'type'. Use 'AI' or 'System'."));
        }

        var detail = await _service.GetDetailAsync(feedbackType, id);
        if (detail is null) return NotFound(ApiResponse<object>.Fail("Feedback not found."));
        return Ok(ApiResponse<FeedbackDetailDto>.Ok(detail));
    }

    /// <summary>Cập nhật feedback (partial). Chỉ Admin.</summary>
    [HttpPut("{type}/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string type, Guid id, [FromBody] UpdateFeedbackRequest request)
    {
        if (!TryParseType(type, out var feedbackType))
        {
            return BadRequest(ApiResponse<object>.Fail("Invalid 'type'. Use 'AI' or 'System'."));
        }

        try
        {
            var detail = await _service.UpdateAsync(feedbackType, id, request);
            if (detail is null) return NotFound(ApiResponse<object>.Fail("Feedback not found."));
            return Ok(ApiResponse<FeedbackDetailDto>.Ok(detail, "Feedback updated successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Xoá mềm feedback. Chỉ Admin.</summary>
    [HttpDelete("{type}/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string type, Guid id)
    {
        if (!TryParseType(type, out var feedbackType))
        {
            return BadRequest(ApiResponse<object>.Fail("Invalid 'type'. Use 'AI' or 'System'."));
        }

        var ok = await _service.SoftDeleteAsync(feedbackType, id);
        if (!ok) return NotFound(ApiResponse<object>.Fail("Feedback not found or already deleted."));
        return Ok(ApiResponse<object>.Ok(new { id, type = feedbackType.ToString() }, "Feedback deleted (soft) successfully."));
    }

    /// <summary>Admin trả lời một System feedback. Chỉ Admin.</summary>
    [HttpPost("system/{id:guid}/reply")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reply(Guid id, [FromBody] ReplyFeedbackRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Reply content is required."));
        }

        var adminId = GetAccountId();
        if (adminId is null) return Unauthorized();

        var reply = await _service.ReplyAsync(id, adminId.Value, request.ReplyContent.Trim());
        if (reply is null) return NotFound(ApiResponse<object>.Fail("System feedback not found or already deleted."));
        return Ok(ApiResponse<FeedbackReplyDto>.Ok(reply, "Reply added successfully."));
    }

    // ---------- Helpers ----------

    private Guid? GetAccountId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static bool TryParseType(string? type, out FeedbackType result)
    {
        result = FeedbackType.System;
        if (string.Equals(type, "ai", StringComparison.OrdinalIgnoreCase)) { result = FeedbackType.AI; return true; }
        if (string.Equals(type, "system", StringComparison.OrdinalIgnoreCase)) { result = FeedbackType.System; return true; }
        return false;
    }

    private static bool TryParseTypeFilter(string? type, out FeedbackType? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(type)) return true;                 // không chọn -> tất cả
        if (string.Equals(type, "ai", StringComparison.OrdinalIgnoreCase)) { result = FeedbackType.AI; return true; }
        if (string.Equals(type, "system", StringComparison.OrdinalIgnoreCase)) { result = FeedbackType.System; return true; }
        return false;
    }
}

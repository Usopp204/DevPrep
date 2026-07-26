using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FeedbackAPI.DTOs;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeedbackAPI.Controllers;

/// <summary>
/// Quản lý System Feedback (góp ý/báo lỗi hệ thống) cho Admin/Manager.
/// Đọc (View/Search/Detail): Admin + Manager. Ghi (Delete/Reply/Edit reply/Delete reply): Admin.
/// </summary>
[ApiController]
[Route("api/admin/feedbacks/system")]
[Authorize(Roles = "Admin,Manager")]
public class AdminSystemFeedbackController : ControllerBase
{
    private readonly ISystemFeedbackService _service;

    public AdminSystemFeedbackController(ISystemFeedbackService service)
    {
        _service = service;
    }

    /// <summary>View + Search System feedback (danh sách đầy đủ, kèm replies). Tất cả tham số lọc đều tuỳ chọn. Sắp xếp mới nhất trước.</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] SystemFeedbackQuery query)
    {
        var result = await _service.GetListAsync(query);
        return Ok(ApiResponse<PagedResult<SystemFeedbackListItemDto>>.Ok(result));
    }

    /// <summary>Xoá mềm một System feedback. Chỉ Admin.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ok = await _service.SoftDeleteAsync(id);
        if (!ok) return NotFound(ApiResponse<object>.Fail("System feedback not found or already deleted."));
        return Ok(ApiResponse<object>.Ok(new { id }, "System feedback deleted (soft) successfully."));
    }

    /// <summary>Khôi phục một System feedback đã xoá mềm. Chỉ Admin.</summary>
    [HttpPost("{id:guid}/restore")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Restore(Guid id)
    {
        var ok = await _service.RestoreAsync(id);
        if (!ok) return NotFound(ApiResponse<object>.Fail("System feedback not found or not deleted."));
        return Ok(ApiResponse<object>.Ok(new { id }, "System feedback restored successfully."));
    }

    /// <summary>Admin thêm một trả lời cho System feedback. Chỉ Admin.</summary>
    [HttpPost("{id:guid}/replies")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddReply(Guid id, [FromBody] ReplyFeedbackRequest request)
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

    /// <summary>Admin sửa nội dung một trả lời. Chỉ Admin.</summary>
    [HttpPut("{id:guid}/replies/{replyId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateReply(Guid id, Guid replyId, [FromBody] ReplyFeedbackRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Reply content is required."));
        }

        var reply = await _service.UpdateReplyAsync(id, replyId, request.ReplyContent.Trim());
        if (reply is null) return NotFound(ApiResponse<object>.Fail("Reply or system feedback not found."));
        return Ok(ApiResponse<FeedbackReplyDto>.Ok(reply, "Reply updated successfully."));
    }

    /// <summary>Admin xoá một trả lời. Chỉ Admin.</summary>
    [HttpDelete("{id:guid}/replies/{replyId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteReply(Guid id, Guid replyId)
    {
        var ok = await _service.DeleteReplyAsync(id, replyId);
        if (!ok) return NotFound(ApiResponse<object>.Fail("Reply or system feedback not found."));
        return Ok(ApiResponse<object>.Ok(new { id, replyId }, "Reply deleted successfully."));
    }

    // ---------- Helpers ----------

    private Guid? GetAccountId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}

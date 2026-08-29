using FeedbackAPI.DTOs;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeedbackAPI.Controllers;

/// <summary>
/// Quản lý AI Feedback (đánh giá buổi phỏng vấn AI) cho Admin/Manager.
/// Đọc (View/Search/Detail): Admin + Manager. Ghi (Delete): Admin.
/// </summary>
[ApiController]
[Route("api/admin/feedbacks/ai")]
[Authorize(Roles = "Admin,Manager")]
public class AdminAiFeedbackController : ControllerBase
{
    private readonly IAiFeedbackService _service;

    public AdminAiFeedbackController(IAiFeedbackService service)
    {
        _service = service;
    }

    /// <summary>View + Search AI feedback (danh sách đầy đủ). Tất cả tham số lọc đều tuỳ chọn. Sắp xếp mới nhất trước.</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] AiFeedbackQuery query)
    {
        var result = await _service.GetListAsync(query);
        return Ok(ApiResponse<PagedResult<AiFeedbackListItemDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _service.GetByIdAsync(id);
        if (item is null) return NotFound(ApiResponse<AiFeedbackListItemDto>.Fail("AI feedback not found."));
        return Ok(ApiResponse<AiFeedbackListItemDto>.Ok(item));
    }

    /// <summary>Xoá mềm một AI feedback. Chỉ Admin.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ok = await _service.SoftDeleteAsync(id);
        if (!ok) return NotFound(ApiResponse<object>.Fail("AI feedback not found or already deleted."));
        return Ok(ApiResponse<object>.Ok(new { id }, "AI feedback deleted (soft) successfully."));
    }

    /// <summary>Khôi phục một AI feedback đã xoá mềm. Chỉ Admin.</summary>
    [HttpPost("{id:guid}/restore")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Restore(Guid id)
    {
        bool ok;
        try
        {
            ok = await _service.RestoreAsync(id);
        }
        catch (FeedbackAPI.Services.FeedbackEligibilityException exception)
        {
            return Conflict(ApiResponse<object>.Fail(exception.Message));
        }
        if (!ok) return NotFound(ApiResponse<object>.Fail("AI feedback not found or not deleted."));
        return Ok(ApiResponse<object>.Ok(new { id }, "AI feedback restored successfully."));
    }
}

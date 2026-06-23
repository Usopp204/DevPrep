using InterviewAPI.DTOs;
using InterviewAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewAPI.Controllers;

/// <summary>
/// Quản lý phiên phỏng vấn cho Admin/Manager.
/// Đọc (View list/search + detail): Admin + Manager. Ghi (Delete): chỉ Admin.
/// Mỗi phiên kèm danh tính người dùng (Email + FullName resolve từ AuthAPI/UserAPI);
/// detail kèm đoạn chat (câu hỏi của AI + câu trả lời của người dùng).
/// </summary>
[ApiController]
[Route("api/admin/interviews")]
[Authorize(Roles = "Admin,Manager")]
public class AdminInterviewController : ControllerBase
{
    private readonly IInterviewService _service;

    public AdminInterviewController(IInterviewService service)
    {
        _service = service;
    }

    /// <summary>View + Search phiên phỏng vấn. Tất cả tham số lọc đều tuỳ chọn. Sắp xếp mới nhất trước.</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] InterviewQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetListAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<InterviewListItemDto>>.Ok(result));
    }

    /// <summary>Chi tiết phiên phỏng vấn, kèm đoạn chat (câu hỏi AI + câu trả lời người dùng).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _service.GetDetailAsync(id, cancellationToken);
        if (detail is null) return NotFound(ApiResponse<object>.Fail("Interview session not found."));
        return Ok(ApiResponse<InterviewDetailDto>.Ok(detail));
    }

    /// <summary>Xoá mềm một phiên phỏng vấn. Chỉ Admin.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _service.SoftDeleteAsync(id, cancellationToken);
        if (!ok) return NotFound(ApiResponse<object>.Fail("Interview session not found or already deleted."));
        return Ok(ApiResponse<object>.Ok(new { id }, "Interview session deleted (soft) successfully."));
    }
}

using FeedbackAPI.DTOs;

namespace FeedbackAPI.Services.Interfaces;

/// <summary>
/// Nghiệp vụ quản lý AI feedback cho Admin: View (list + search), Delete (soft).
/// </summary>
public interface IAiFeedbackService
{
    /// <summary>Danh sách AI feedback đầy đủ (kèm lọc/tìm kiếm), sắp xếp CreatedAt giảm dần.</summary>
    Task<PagedResult<AiFeedbackListItemDto>> GetListAsync(AiFeedbackQuery query);

    /// <summary>Xoá mềm (IsDeleted = true). Trả false nếu không tìm thấy hoặc đã xoá.</summary>
    Task<bool> SoftDeleteAsync(Guid id);

    /// <summary>Khôi phục bản ghi đã xoá mềm (IsDeleted = false). Trả false nếu không tìm thấy hoặc chưa bị xoá.</summary>
    Task<bool> RestoreAsync(Guid id);
}

using FeedbackAPI.DTOs;

namespace FeedbackAPI.Services.Interfaces;

/// <summary>
/// Nghiệp vụ quản lý System feedback cho Admin: View (list + search, kèm replies),
/// Delete (soft), Reply, Edit reply, Delete reply.
/// </summary>
public interface ISystemFeedbackService
{
    /// <summary>Danh sách System feedback đầy đủ (kèm replies + lọc/tìm kiếm), sắp xếp CreatedAt giảm dần.</summary>
    Task<PagedResult<SystemFeedbackListItemDto>> GetListAsync(SystemFeedbackQuery query);

    /// <summary>Xoá mềm (IsDeleted = true). Trả false nếu không tìm thấy hoặc đã xoá.</summary>
    Task<bool> SoftDeleteAsync(Guid id);

    /// <summary>Khôi phục bản ghi đã xoá mềm (IsDeleted = false). Trả false nếu không tìm thấy hoặc chưa bị xoá.</summary>
    Task<bool> RestoreAsync(Guid id);

    /// <summary>Admin thêm một trả lời cho System feedback. Trả null nếu feedback không tồn tại/đã xoá.</summary>
    Task<FeedbackReplyDto?> ReplyAsync(Guid feedbackId, Guid repliedBy, string replyContent);

    /// <summary>Sửa nội dung một trả lời. Trả null nếu feedback/reply không tồn tại (hoặc feedback đã xoá).</summary>
    Task<FeedbackReplyDto?> UpdateReplyAsync(Guid feedbackId, Guid replyId, string replyContent);

    /// <summary>Xoá hẳn một trả lời. Trả false nếu feedback/reply không tồn tại (hoặc feedback đã xoá).</summary>
    Task<bool> DeleteReplyAsync(Guid feedbackId, Guid replyId);

    /// <summary>Lấy chi tiết một System feedback (kèm replies).</summary>
    Task<SystemFeedbackListItemDto?> GetDetailAsync(Guid id);
}

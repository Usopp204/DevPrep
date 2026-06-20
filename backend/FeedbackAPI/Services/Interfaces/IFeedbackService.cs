using FeedbackAPI.DTOs;

namespace FeedbackAPI.Services.Interfaces;

/// <summary>
/// Nghiệp vụ quản lý feedback cho Admin: list (lọc AI/System/all, mới nhất trước),
/// detail, update, soft-delete, reply.
/// </summary>
public interface IFeedbackService
{
    /// <summary>
    /// Lấy danh sách feedback. type = null -> tất cả (gộp System + AI), sắp xếp CreatedAt giảm dần.
    /// type = System -> chỉ SystemFeedbacks; type = AI -> chỉ InterviewFeedbacks.
    /// </summary>
    Task<PagedResult<FeedbackListItemDto>> GetListAsync(FeedbackType? type, int page, int pageSize);

    Task<FeedbackDetailDto?> GetDetailAsync(FeedbackType type, Guid id);

    /// <summary>Cập nhật feedback. Ném ArgumentException nếu giá trị Status/Category/Rating không hợp lệ.</summary>
    Task<FeedbackDetailDto?> UpdateAsync(FeedbackType type, Guid id, UpdateFeedbackRequest request);

    /// <summary>Xoá mềm (IsDeleted = true). Trả false nếu không tìm thấy hoặc đã xoá.</summary>
    Task<bool> SoftDeleteAsync(FeedbackType type, Guid id);

    /// <summary>Admin trả lời một System feedback. Trả null nếu feedback không tồn tại/đã xoá.</summary>
    Task<FeedbackReplyDto?> ReplyAsync(Guid feedbackId, Guid repliedBy, string replyContent);
}

using FeedbackAPI.Models;

namespace FeedbackAPI.Repositories.Interfaces;

/// <summary>
/// Truy cập dữ liệu cho System feedback (bảng SystemFeedbacks) và phần trả lời (FeedbackReplies).
/// Query() trả về IQueryable đã lọc bỏ bản ghi soft-deleted (IsDeleted = 0), AsNoTracking.
/// </summary>
public interface ISystemFeedbackRepository
{
    IQueryable<SystemFeedback> Query();

    /// <summary>Như Query() nhưng KÈM cả bản ghi đã xoá mềm (cho admin xem/khôi phục).</summary>
    IQueryable<SystemFeedback> QueryIncludingDeleted();

    Task<SystemFeedback?> GetByIdAsync(Guid id, bool includeReplies = false, bool tracking = false);

    Task AddReplyAsync(FeedbackReply reply);
    void RemoveReply(FeedbackReply reply);

    Task SaveChangesAsync();
}

using FeedbackAPI.Models;

namespace FeedbackAPI.Repositories.Interfaces;

/// <summary>
/// Truy cập dữ liệu cho AI feedback (bảng InterviewFeedbacks).
/// Query() trả về IQueryable đã lọc bỏ bản ghi soft-deleted (IsDeleted = 0), AsNoTracking.
/// </summary>
public interface IAiFeedbackRepository
{
    IQueryable<InterviewFeedback> Query();

    /// <summary>Như Query() nhưng KÈM cả bản ghi đã xoá mềm (cho admin xem/khôi phục).</summary>
    IQueryable<InterviewFeedback> QueryIncludingDeleted();

    Task<InterviewFeedback?> GetByIdAsync(Guid id, bool tracking = false);
    Task SaveChangesAsync();
}

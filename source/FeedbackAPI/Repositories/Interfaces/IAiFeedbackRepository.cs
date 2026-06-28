using FeedbackAPI.Models;

namespace FeedbackAPI.Repositories.Interfaces;

/// <summary>
/// Truy cập dữ liệu cho AI feedback (bảng InterviewFeedbacks).
/// Query() trả về IQueryable đã lọc bỏ bản ghi soft-deleted (IsDeleted = 0), AsNoTracking.
/// </summary>
public interface IAiFeedbackRepository
{
    IQueryable<InterviewFeedback> Query();
    Task<InterviewFeedback?> GetByIdAsync(Guid id, bool tracking = false);
    Task SaveChangesAsync();
}

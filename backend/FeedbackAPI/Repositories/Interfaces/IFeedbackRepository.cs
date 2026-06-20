using FeedbackAPI.Models;

namespace FeedbackAPI.Repositories.Interfaces;

/// <summary>
/// Truy cập dữ liệu cho 2 loại feedback (System + Interview/AI) và phần trả lời.
/// Các Query* trả về IQueryable đã lọc bỏ bản ghi soft-deleted (IsDeleted = 0).
/// </summary>
public interface IFeedbackRepository
{
    IQueryable<SystemFeedback> QuerySystem();
    IQueryable<InterviewFeedback> QueryInterview();

    Task<SystemFeedback?> GetSystemByIdAsync(Guid id, bool includeReplies = false, bool tracking = false);
    Task<InterviewFeedback?> GetInterviewByIdAsync(Guid id, bool tracking = false);

    Task AddReplyAsync(FeedbackReply reply);
    Task SaveChangesAsync();
}

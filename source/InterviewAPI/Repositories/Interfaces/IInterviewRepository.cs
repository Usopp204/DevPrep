using InterviewAPI.Models;

namespace InterviewAPI.Repositories.Interfaces;

/// <summary>
/// Truy cập dữ liệu phiên phỏng vấn. Query() trả về IQueryable đã lọc bỏ bản ghi soft-deleted, AsNoTracking.
/// </summary>
public interface IInterviewRepository
{
    IQueryable<InterviewSession> Query();

    /// <summary>Lấy 1 phiên (không kèm chat) — dùng cho soft delete.</summary>
    Task<InterviewSession?> GetByIdAsync(Guid id, bool tracking = false, CancellationToken cancellationToken = default);

    /// <summary>Lấy 1 phiên kèm câu hỏi + tin nhắn chat (cho màn detail). Bỏ bản ghi đã xoá mềm.</summary>
    Task<InterviewSession?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

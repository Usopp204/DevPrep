using InterviewAPI.DTOs;

namespace InterviewAPI.Services.Interfaces;

/// <summary>
/// Nghiệp vụ quản lý phiên phỏng vấn cho Admin: View (list + search), View detail (kèm đoạn chat),
/// Delete (soft). Danh sách/chi tiết được resolve thêm Email + FullName của người dùng.
/// </summary>
public interface IInterviewService
{
    /// <summary>Danh sách phiên phỏng vấn (kèm lọc/tìm kiếm), sắp xếp CreatedAt giảm dần.</summary>
    Task<PagedResult<InterviewListItemDto>> GetListAsync(InterviewQuery query, CancellationToken cancellationToken = default);

    Task<PagedResult<ManagerInterviewSessionListItemDto>> GetManagerSessionListAsync(InterviewQuery query, CancellationToken cancellationToken = default);

    /// <summary>Chi tiết phiên phỏng vấn kèm đoạn chat (câu hỏi AI + câu trả lời). Null nếu không tồn tại/đã xoá.</summary>
    Task<InterviewDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ManagerInterviewSessionLogDto?> GetManagerSessionLogAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Xoá mềm (IsDeleted = true). Trả false nếu không tìm thấy hoặc đã xoá.</summary>
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

using FeedbackAPI.DTOs;

namespace FeedbackAPI.Services.Interfaces;

/// <summary>
/// Interview feedback workflows for admin management, public reviews, and Candidate submissions.
/// </summary>
public interface IAiFeedbackService
{
    Task<PagedResult<AiFeedbackListItemDto>> GetListAsync(AiFeedbackQuery query);
    Task<AiFeedbackListItemDto?> GetByIdAsync(Guid id);
    Task<PagedResult<PublicAiFeedbackDto>> GetPublicReviewsAsync(
        PublicFeedbackQuery query,
        Guid? viewerUserId,
        CancellationToken ct = default);
    Task<MyInterviewFeedbackDto?> GetMyFeedbackAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
    Task<CreatedInterviewFeedbackDto> SubmitAsync(Guid userId, SubmitInterviewFeedbackRequest request, string? bearerToken, CancellationToken ct = default);
    Task EditAsync(Guid feedbackId, Guid userId, UpdateInterviewFeedbackRequest request, CancellationToken ct = default);
    Task CandidateDeleteAsync(Guid feedbackId, Guid userId, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(Guid id);

    /// <summary>Khôi phục bản ghi đã xoá mềm (IsDeleted = false). Trả false nếu không tìm thấy hoặc chưa bị xoá.</summary>
    Task<bool> RestoreAsync(Guid id);
}

using FeedbackAPI.DTOs;

namespace FeedbackAPI.Services.Interfaces;

/// <summary>
/// System feedback workflows for admin management, restore, public reviews, and Candidate submissions.
/// </summary>
public interface ISystemFeedbackService
{
    Task<PagedResult<SystemFeedbackListItemDto>> GetListAsync(SystemFeedbackQuery query);
    Task<SystemFeedbackListItemDto?> GetDetailAsync(Guid id);
    Task<PagedResult<PublicSystemFeedbackDto>> GetPublicReviewsAsync(PublicFeedbackQuery query, CancellationToken ct = default);
    Task<SystemFeedbackSubmissionStatusDto> GetSubmissionStatusAsync(Guid userId, CancellationToken ct = default);
    Task<MySystemFeedbackDto?> GetMyFeedbackAsync(Guid feedbackId, Guid userId, CancellationToken ct = default);
    Task<CreatedSystemFeedbackDto> SubmitAsync(Guid userId, SubmitSystemFeedbackRequest request, string? bearerToken, CancellationToken ct = default);
    Task EditAsync(Guid feedbackId, Guid userId, UpdateSystemFeedbackRequest request, CancellationToken ct = default);
    Task CandidateDeleteAsync(Guid feedbackId, Guid userId, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(Guid id);
    Task<bool> RestoreAsync(Guid id);
    Task<FeedbackReplyDto?> ReplyAsync(Guid feedbackId, Guid repliedBy, string replyContent);
    Task<FeedbackReplyDto?> UpdateReplyAsync(Guid feedbackId, Guid replyId, string replyContent);
    Task<bool> DeleteReplyAsync(Guid feedbackId, Guid replyId);
}
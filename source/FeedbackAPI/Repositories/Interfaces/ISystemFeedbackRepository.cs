using FeedbackAPI.Models;

namespace FeedbackAPI.Repositories.Interfaces;

/// <summary>
/// Data access for system feedback, replies, public reviews, deleted records, and Candidate submissions.
/// </summary>
public interface ISystemFeedbackRepository
{
    IQueryable<SystemFeedback> Query();
    IQueryable<SystemFeedback> QueryIncludingDeleted();
    IQueryable<SystemFeedback> PublicQuery();
    Task<SystemFeedback?> GetByIdAsync(Guid id, bool includeReplies = false, bool tracking = false, CancellationToken ct = default);
    Task<SystemFeedback?> GetOwnedActiveByIdAsync(Guid feedbackId, Guid userId, bool tracking = false, CancellationToken ct = default);
    Task<int> CountByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<SystemFeedback?> GetLatestByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task ResolvePendingReportsAsync(Guid systemFeedbackId, CancellationToken ct = default);
    Task<bool> TryAddActiveAsync(SystemFeedback feedback, CancellationToken ct = default);
    Task<FeedbackRestoreOutcome> RestoreAsync(Guid feedbackId, DateTime restoredAt, CancellationToken ct = default);
    Task AddReplyAsync(FeedbackReply reply, CancellationToken ct = default);
    void RemoveReply(FeedbackReply reply);
    Task SaveChangesAsync(CancellationToken ct = default);
}
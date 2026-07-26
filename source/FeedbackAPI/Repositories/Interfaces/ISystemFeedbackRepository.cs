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
    Task<int> CountByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<SystemFeedback?> GetLatestByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(SystemFeedback feedback, CancellationToken ct = default);
    Task AddReplyAsync(FeedbackReply reply, CancellationToken ct = default);
    void RemoveReply(FeedbackReply reply);
    Task SaveChangesAsync(CancellationToken ct = default);
}
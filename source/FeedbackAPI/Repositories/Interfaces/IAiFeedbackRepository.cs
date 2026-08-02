using FeedbackAPI.Models;

namespace FeedbackAPI.Repositories.Interfaces;

/// <summary>
/// Data access for interview feedback, including admin, public, deleted-record, and Candidate submission queries.
/// </summary>
public interface IAiFeedbackRepository
{
    IQueryable<InterviewFeedback> Query();
    IQueryable<InterviewFeedback> QueryIncludingDeleted();
    IQueryable<InterviewFeedback> PublicQuery();
    Task<InterviewFeedback?> GetByIdAsync(Guid id, bool tracking = false, CancellationToken ct = default);
    Task<InterviewFeedback?> GetOwnedActiveByIdAsync(Guid feedbackId, Guid userId, bool tracking = false, CancellationToken ct = default);
    Task<InterviewFeedback?> GetByUserAndSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
    Task<bool> ActiveExistsAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
    Task ResolvePendingReportsAsync(Guid interviewFeedbackId, CancellationToken ct = default);
    Task<bool> TryAddActiveAsync(InterviewFeedback feedback, CancellationToken ct = default);
    Task<FeedbackRestoreOutcome> RestoreAsync(Guid feedbackId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
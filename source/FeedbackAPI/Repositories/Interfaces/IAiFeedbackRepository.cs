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
    Task<InterviewFeedback?> GetByUserAndSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
    Task<bool> ActiveExistsAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
    Task AddAsync(InterviewFeedback feedback, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
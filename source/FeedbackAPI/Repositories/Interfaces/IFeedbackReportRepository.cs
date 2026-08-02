using FeedbackAPI.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace FeedbackAPI.Repositories.Interfaces;

public sealed record FeedbackReportViewerState(Guid FeedbackId, bool IsOwner, bool HasReported);

public interface IFeedbackReportRepository
{
    IQueryable<FeedbackReport> Query();
    IQueryable<FeedbackReport> TrackingQuery();
    Task<SystemFeedback?> GetSystemFeedbackAsync(Guid feedbackId, bool tracking, CancellationToken ct = default);
    Task<bool> ExistsSystemAsync(Guid feedbackId, Guid reporterUserId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, FeedbackReportViewerState>> GetSystemViewerStatesAsync(
        IReadOnlyCollection<Guid> feedbackIds,
        Guid viewerUserId,
        CancellationToken ct = default);
    Task AddAsync(FeedbackReport report, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IDbContextTransaction?> BeginSerializableTransactionAsync(CancellationToken ct = default);
}

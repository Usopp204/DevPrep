using FeedbackAPI.DTOs;

namespace FeedbackAPI.Services.Interfaces;

public interface IFeedbackReportService
{
    Task<CreatedFeedbackReportDto> CreateAsync(
        string feedbackType,
        Guid feedbackId,
        Guid reporterUserId,
        CreateFeedbackReportRequest request,
        CancellationToken ct = default);

    Task ApplySystemViewerStateAsync(
        IReadOnlyList<PublicSystemFeedbackDto> feedback,
        Guid? viewerUserId,
        bool isCandidate,
        CancellationToken ct = default);


    Task<PagedResult<FeedbackReportListItemDto>> GetListAsync(
        FeedbackReportQuery query,
        CancellationToken ct = default);

    Task<FeedbackReportDetailsDto?> GetDetailsAsync(Guid reportId, CancellationToken ct = default);

    Task<FeedbackReportResolutionDto> DismissAsync(
        Guid reportId,
        Guid reviewedBy,
        ResolveFeedbackReportRequest request,
        CancellationToken ct = default);

    Task<FeedbackReportResolutionDto> DeleteFeedbackAsync(
        Guid reportId,
        Guid reviewedBy,
        ResolveFeedbackReportRequest request,
        CancellationToken ct = default);
}

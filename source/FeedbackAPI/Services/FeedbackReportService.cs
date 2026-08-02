using FeedbackAPI.DTOs;
using FeedbackAPI.Helpers;
using FeedbackAPI.Models;
using FeedbackAPI.Repositories.Interfaces;
using FeedbackAPI.Services.Interfaces;
using FeedbackAPI.Validation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Services;

public sealed class FeedbackReportService(IFeedbackReportRepository repository) : IFeedbackReportService
{
    private const int MaxPageSize = 100;

    public async Task<CreatedFeedbackReportDto> CreateAsync(
        string feedbackType,
        Guid feedbackId,
        Guid reporterUserId,
        CreateFeedbackReportRequest request,
        CancellationToken ct = default)
    {
        if (reporterUserId == Guid.Empty)
        {
            throw new FeedbackReportForbiddenException("A Candidate account is required to report feedback.");
        }

        if (feedbackType == FeedbackReportType.Interview)
        {
            throw new FeedbackReportForbiddenException("AI feedback cannot be reported. Only system feedback can be reported.");
        }

        ValidateFeedbackType(feedbackType);
        if (feedbackId == Guid.Empty)
        {
            throw new FeedbackReportNotFoundException("The feedback is no longer publicly available.");
        }

        await using var transaction = await repository.BeginSerializableTransactionAsync(ct);
        var ownerUserId = await GetPublicSystemTargetOwnerAsync(feedbackId, ct);
        if (ownerUserId == reporterUserId)
        {
            throw new FeedbackReportForbiddenException("You cannot report your own feedback.");
        }

        ValidateReason(request.ReasonCode);
        var details = FeedbackReportInput.NormalizeOptional(
            request.Details,
            FeedbackReportInput.MaxDetailsLength,
            "Details");
        if (request.ReasonCode == FeedbackReportReason.Other && details is null)
        {
            throw new FeedbackReportValidationException("Details are required when the reason is Other.");
        }

        if (await repository.ExistsSystemAsync(feedbackId, reporterUserId, ct))
        {
            throw new DuplicateFeedbackReportException();
        }

        var report = new FeedbackReport
        {
            FeedbackReportId = Guid.NewGuid(),
            FeedbackType = FeedbackReportType.System,
            SystemFeedbackId = feedbackId,
            ReporterUserId = reporterUserId,
            ReasonCode = request.ReasonCode,
            Details = details,
            Status = FeedbackReportStatus.Pending,
            CreatedAt = VietnamTime.Now
        };

        await repository.AddAsync(report, ct);
        try
        {
            await repository.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct);
            }

            if (await repository.ExistsSystemAsync(feedbackId, reporterUserId, ct))
            {
                throw new DuplicateFeedbackReportException();
            }

            throw;
        }

        return new CreatedFeedbackReportDto
        {
            ReportId = report.FeedbackReportId,
            Status = report.Status,
            CreatedAt = report.CreatedAt,
            Message = "Thank you. Your report has been submitted for review."
        };
    }

    public async Task ApplySystemViewerStateAsync(
        IReadOnlyList<PublicSystemFeedbackDto> feedback,
        Guid? viewerUserId,
        bool isCandidate,
        CancellationToken ct = default)
    {
        if (!isCandidate || !viewerUserId.HasValue || viewerUserId == Guid.Empty)
        {
            foreach (var item in feedback)
            {
                item.CanReport = false;
                item.HasReported = false;
                item.IsOwner = false;
            }

            return;
        }

        var ids = feedback.Select(item => item.FeedbackId).ToArray();
        var states = await repository.GetSystemViewerStatesAsync(ids, viewerUserId.Value, ct);
        foreach (var item in feedback)
        {
            if (!states.TryGetValue(item.FeedbackId, out var state))
            {
                item.CanReport = false;
                item.HasReported = false;
                item.IsOwner = false;
                continue;
            }

            item.CanReport = !state.IsOwner && !state.HasReported;
            item.HasReported = state.HasReported;
            item.IsOwner = state.IsOwner;
        }
    }

    public async Task<PagedResult<FeedbackReportListItemDto>> GetListAsync(
        FeedbackReportQuery query,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var feedbackType = NormalizeFilter(query.FeedbackType, FeedbackReportType.All, "Feedback type");
        var status = NormalizeFilter(query.Status, FeedbackReportStatus.All, "Status", FeedbackReportStatus.Pending);
        var reasonCode = NormalizeFilter(query.ReasonCode, FeedbackReportReason.Labels.Keys, "Reason code");
        var reports = repository.Query();

        if (feedbackType is not null)
        {
            reports = reports.Where(report => report.FeedbackType == feedbackType);
        }

        if (status is not null)
        {
            reports = reports.Where(report => report.Status == status);
        }

        if (reasonCode is not null)
        {
            reports = reports.Where(report => report.ReasonCode == reasonCode);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            var matchingReasons = FeedbackReportReason.Labels
                .Where(pair => pair.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || pair.Value.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .ToArray();
            reports = reports.Where(report =>
                (report.Details != null && report.Details.Contains(keyword))
                || matchingReasons.Contains(report.ReasonCode)
                || (report.InterviewFeedback != null
                    && report.InterviewFeedback.Content != null
                    && report.InterviewFeedback.Content.Contains(keyword))
                || (report.SystemFeedback != null && report.SystemFeedback.Content.Contains(keyword)));
        }

        var total = await reports.CountAsync(ct);
        var items = await reports
            .OrderByDescending(report => report.CreatedAt)
            .ThenByDescending(report => report.FeedbackReportId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(report => new FeedbackReportListItemDto
            {
                ReportId = report.FeedbackReportId,
                FeedbackType = report.FeedbackType,
                ReasonCode = report.ReasonCode,
                Status = report.Status,
                FeedbackContent = report.FeedbackType == FeedbackReportType.Interview
                    ? report.InterviewFeedback!.Content
                    : report.SystemFeedback!.Content,
                Details = report.Details,
                CreatedAt = report.CreatedAt,
                RelatedReportCount = report.FeedbackType == FeedbackReportType.Interview
                    ? report.InterviewFeedback!.FeedbackReports.Count
                    : report.SystemFeedback!.FeedbackReports.Count,
                IsFeedbackVisible = report.FeedbackType == FeedbackReportType.Interview
                    ? report.InterviewFeedback!.IsVisible
                    : report.SystemFeedback!.IsVisible,
                IsFeedbackDeleted = report.FeedbackType == FeedbackReportType.Interview
                    ? report.InterviewFeedback!.IsDeleted
                    : report.SystemFeedback!.IsDeleted
            })
            .ToListAsync(ct);

        foreach (var item in items)
        {
            item.ReasonLabel = ReasonLabel(item.ReasonCode);
        }

        return new PagedResult<FeedbackReportListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<FeedbackReportDetailsDto?> GetDetailsAsync(Guid reportId, CancellationToken ct = default)
    {
        var report = await repository.Query()
            .SingleOrDefaultAsync(item => item.FeedbackReportId == reportId, ct);
        if (report is null)
        {
            return null;
        }

        var relatedReports = await RelatedQuery(report)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.FeedbackReportId)
            .Select(item => new RelatedFeedbackReportDto
            {
                ReportId = item.FeedbackReportId,
                ReporterUserId = item.ReporterUserId,
                ReasonCode = item.ReasonCode,
                Details = item.Details,
                Status = item.Status,
                CreatedAt = item.CreatedAt,
                ReviewedAt = item.ReviewedAt,
                ReviewedBy = item.ReviewedBy,
                ResolutionNote = item.ResolutionNote
            })
            .ToListAsync(ct);
        foreach (var related in relatedReports)
        {
            related.ReasonLabel = ReasonLabel(related.ReasonCode);
        }

        return MapDetails(report, relatedReports);
    }

    public async Task<FeedbackReportResolutionDto> DismissAsync(
        Guid reportId,
        Guid reviewedBy,
        ResolveFeedbackReportRequest request,
        CancellationToken ct = default)
    {
        ValidateReviewer(reviewedBy);
        var note = FeedbackReportInput.NormalizeOptional(
            request.ResolutionNote,
            FeedbackReportInput.MaxResolutionNoteLength,
            "Resolution note");
        await using var transaction = await repository.BeginSerializableTransactionAsync(ct);
        var report = await repository.TrackingQuery()
            .SingleOrDefaultAsync(item => item.FeedbackReportId == reportId, ct)
            ?? throw new FeedbackReportNotFoundException();
        if (report.Status != FeedbackReportStatus.Pending)
        {
            throw new FeedbackReportConflictException("Only a pending report can be dismissed.");
        }

        var reviewedAt = VietnamTime.Now;
        report.Status = FeedbackReportStatus.Dismissed;
        report.ReviewedAt = reviewedAt;
        report.ReviewedBy = reviewedBy;
        report.ResolutionNote = note;
        await repository.SaveChangesAsync(ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        return ToResolution(report, resolvedReportCount: 1, feedbackDeleted: false);
    }

    public async Task<FeedbackReportResolutionDto> DeleteFeedbackAsync(
        Guid reportId,
        Guid reviewedBy,
        ResolveFeedbackReportRequest request,
        CancellationToken ct = default)
    {
        ValidateReviewer(reviewedBy);
        var note = FeedbackReportInput.NormalizeOptional(
            request.ResolutionNote,
            FeedbackReportInput.MaxResolutionNoteLength,
            "Resolution note");
        await using var transaction = await repository.BeginSerializableTransactionAsync(ct);
        var report = await repository.TrackingQuery()
            .SingleOrDefaultAsync(item => item.FeedbackReportId == reportId, ct)
            ?? throw new FeedbackReportNotFoundException();

        var feedbackAlreadyDeleted = report.FeedbackType == FeedbackReportType.Interview
            ? report.InterviewFeedback?.IsDeleted
            : report.SystemFeedback?.IsDeleted;
        if (!feedbackAlreadyDeleted.HasValue)
        {
            throw new FeedbackReportNotFoundException("The reported feedback no longer exists.");
        }

        if (report.Status != FeedbackReportStatus.Pending && !feedbackAlreadyDeleted.Value)
        {
            throw new FeedbackReportConflictException("Only a pending report can be resolved by deleting feedback.");
        }

        var reviewedAt = VietnamTime.Now;
        if (!feedbackAlreadyDeleted.Value)
        {
            if (report.FeedbackType == FeedbackReportType.Interview)
            {
                report.InterviewFeedback!.IsDeleted = true;
                report.InterviewFeedback.IsVisible = false;
                report.InterviewFeedback.DeletedAt = reviewedAt;
            }
            else
            {
                report.SystemFeedback!.IsDeleted = true;
                report.SystemFeedback.IsVisible = false;
                report.SystemFeedback.DeletedAt = reviewedAt;
                report.SystemFeedback.UpdatedAt = reviewedAt;
            }
        }

        var pendingReports = await RelatedTrackingQuery(report)
            .Where(item => item.Status == FeedbackReportStatus.Pending)
            .ToListAsync(ct);
        foreach (var pending in pendingReports)
        {
            pending.Status = FeedbackReportStatus.ResolvedByFeedbackDeletion;
            pending.ReviewedAt = reviewedAt;
            pending.ReviewedBy = reviewedBy;
            pending.ResolutionNote = note;
        }

        await repository.SaveChangesAsync(ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        return ToResolution(report, pendingReports.Count, feedbackDeleted: true);
    }

    private async Task<Guid> GetPublicSystemTargetOwnerAsync(
        Guid feedbackId,
        CancellationToken ct)
    {
        var target = await repository.GetSystemFeedbackAsync(feedbackId, tracking: false, ct);
        if (target is null || target.IsDeleted || !target.IsVisible)
        {
            throw new FeedbackReportNotFoundException("The feedback is no longer publicly available.");
        }

        return target.UserId;
    }

    private IQueryable<FeedbackReport> RelatedQuery(FeedbackReport report) =>
        repository.Query().Where(item => report.FeedbackType == FeedbackReportType.Interview
            ? item.InterviewFeedbackId == report.InterviewFeedbackId
            : item.SystemFeedbackId == report.SystemFeedbackId);

    private IQueryable<FeedbackReport> RelatedTrackingQuery(FeedbackReport report) =>
        repository.TrackingQuery().Where(item => report.FeedbackType == FeedbackReportType.Interview
            ? item.InterviewFeedbackId == report.InterviewFeedbackId
            : item.SystemFeedbackId == report.SystemFeedbackId);

    private static FeedbackReportDetailsDto MapDetails(
        FeedbackReport report,
        IReadOnlyList<RelatedFeedbackReportDto> relatedReports)
    {
        var interview = report.InterviewFeedback;
        var system = report.SystemFeedback;
        return new FeedbackReportDetailsDto
        {
            ReportId = report.FeedbackReportId,
            FeedbackType = report.FeedbackType,
            ReasonCode = report.ReasonCode,
            ReasonLabel = ReasonLabel(report.ReasonCode),
            Status = report.Status,
            FeedbackContent = interview?.Content ?? system?.Content,
            Details = report.Details,
            CreatedAt = report.CreatedAt,
            RelatedReportCount = relatedReports.Count,
            IsFeedbackVisible = interview?.IsVisible ?? system?.IsVisible ?? false,
            IsFeedbackDeleted = interview?.IsDeleted ?? system?.IsDeleted ?? true,
            ReporterUserId = report.ReporterUserId,
            OverallRating = interview?.Rating ?? system?.Rating,
            QuestionQualityRating = interview?.QuestionQualityRating,
            AiAccuracyRating = interview?.AiAccuracyRating,
            ScoringFairnessRating = interview?.ScoringFairnessRating,
            FeedbackUsefulnessRating = interview?.FeedbackUsefulnessRating,
            UiUxRating = system?.UiUxRating,
            UsabilityRating = system?.UsabilityRating,
            FeatureQualityRating = system?.FeatureQualityRating,

            ReviewedAt = report.ReviewedAt,
            ReviewedBy = report.ReviewedBy,
            ResolutionNote = report.ResolutionNote,
            RelatedReports = relatedReports
        };
    }

    private static FeedbackReportResolutionDto ToResolution(
        FeedbackReport report,
        int resolvedReportCount,
        bool feedbackDeleted) => new()
        {
            ReportId = report.FeedbackReportId,
            Status = report.Status,
            ReviewedAt = report.ReviewedAt,
            ReviewedBy = report.ReviewedBy,
            ResolutionNote = report.ResolutionNote,
            ResolvedReportCount = resolvedReportCount,
            FeedbackDeleted = feedbackDeleted
        };

    private static string? NormalizeFilter(
        string? value,
        IEnumerable<string> allowed,
        string label,
        string? defaultValue = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (value == "All")
        {
            return null;
        }

        if (!allowed.Contains(value, StringComparer.Ordinal))
        {
            throw new FeedbackReportValidationException($"{label} is invalid.");
        }

        return value;
    }

    private static void ValidateFeedbackType(string feedbackType)
    {
        if (!FeedbackReportType.All.Contains(feedbackType))
        {
            throw new FeedbackReportValidationException("Feedback type is invalid.");
        }
    }

    private static void ValidateReason(string? reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            throw new FeedbackReportValidationException("Reason code is required.");
        }

        if (!FeedbackReportReason.Labels.ContainsKey(reasonCode))
        {
            throw new FeedbackReportValidationException("Reason code is invalid.");
        }
    }

    private static void ValidateReviewer(Guid reviewedBy)
    {
        if (reviewedBy == Guid.Empty)
        {
            throw new FeedbackReportForbiddenException("An Admin account is required.");
        }
    }

    private static string ReasonLabel(string reasonCode) =>
        FeedbackReportReason.Labels.TryGetValue(reasonCode, out var label) ? label : reasonCode;

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException && sqlException.Number is 2601 or 2627)
            {
                return true;
            }
        }

        return false;
    }
}

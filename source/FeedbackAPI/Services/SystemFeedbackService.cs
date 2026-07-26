using FeedbackAPI.Clients;
using FeedbackAPI.DTOs;
using FeedbackAPI.Helpers;
using FeedbackAPI.Models;
using FeedbackAPI.Repositories.Interfaces;
using FeedbackAPI.Services.Interfaces;
using FeedbackAPI.Validation;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Services;

public class SystemFeedbackService : ISystemFeedbackService
{
    private const int MaxAdminPageSize = 100;
    private const int MaxPublicPageSize = 50;

    private const string NewStatus = "New";
    private const string RepliedStatus = "Replied";
    private const string Pending = "Pending";
    private const string Approved = "Approved";
    private const string Rejected = "Rejected";

    private readonly ISystemFeedbackRepository _repository;
    private readonly IUserApiClient _userApiClient;
    private readonly IAuthApiClient _authApiClient;
    private readonly IInterviewApiClient _interviewApiClient;

    public SystemFeedbackService(
        ISystemFeedbackRepository repository,
        IUserApiClient userApiClient,
        IAuthApiClient authApiClient,
        IInterviewApiClient interviewApiClient)
    {
        _repository = repository;
        _userApiClient = userApiClient;
        _authApiClient = authApiClient;
        _interviewApiClient = interviewApiClient;
    }

    public async Task<PagedResult<SystemFeedbackListItemDto>> GetListAsync(SystemFeedbackQuery query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, MaxAdminPageSize);
        var skip = (page - 1) * pageSize;

        // OnlyDeleted = true  -> CHỈ bản ghi đã xoá mềm (trang "Show deleted" của admin).
        // IncludeDeleted = true -> kèm cả bản ghi đã xoá mềm.
        // mặc định -> chỉ bản ghi chưa xoá.
        var q = query.OnlyDeleted
            ? _repository.QueryIncludingDeleted().Where(f => f.IsDeleted)
            : query.IncludeDeleted ? _repository.QueryIncludingDeleted() : _repository.Query();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            var nameIds = await _userApiClient.SearchIdsByNameAsync(kw);
            var emailIds = await _authApiClient.SearchIdsByEmailAsync(kw);
            var matchIds = nameIds.Concat(emailIds).Distinct().ToList();
            q = q.Where(f => (f.Subject != null && f.Subject.Contains(kw)) || f.Content.Contains(kw) || matchIds.Contains(f.UserId));
        }
        if (!string.IsNullOrWhiteSpace(query.Category)) q = q.Where(f => f.Category == query.Category);
        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(f => f.Status == query.Status);
        if (query.Rating.HasValue) q = q.Where(f => f.Rating == query.Rating.Value);
        if (query.UserId.HasValue) q = q.Where(f => f.UserId == query.UserId.Value);
        if (query.DateFrom.HasValue) q = q.Where(f => f.CreatedAt >= query.DateFrom.Value);
        if (query.DateTo.HasValue) q = q.Where(f => f.CreatedAt <= query.DateTo.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(f => f.CreatedAt)
            .Skip(skip).Take(pageSize)
            .Select(f => new SystemFeedbackListItemDto
            {
                Id = f.FeedbackId,
                UserId = f.UserId,
                Category = f.Category,
                Subject = f.Subject,
                Content = f.Content,
                Rating = f.Rating,
                UiUxRating = f.UiUxRating,
                UsabilityRating = f.UsabilityRating,
                FeatureQualityRating = f.FeatureQualityRating,
                Status = f.Status,
                ModerationStatus = f.ModerationStatus,
                IsVisible = f.IsVisible,
                ModeratedAt = f.ModeratedAt,
                ModeratedBy = f.ModeratedBy,
                ReplyCount = f.FeedbackReplies.Count,
                IsDeleted = f.IsDeleted,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt,
                Replies = f.FeedbackReplies.OrderBy(r => r.CreatedAt).Select(r => new FeedbackReplyDto
                {
                    ReplyId = r.ReplyId,
                    FeedbackId = r.FeedbackId,
                    RepliedBy = r.RepliedBy,
                    ReplyContent = r.ReplyContent,
                    CreatedAt = r.CreatedAt
                }).ToList()
            })
            .ToListAsync();

        await ResolveUsersAsync(items);
        return ToPaged(items, page, pageSize, total);
    }

    public async Task<PagedResult<PublicSystemFeedbackDto>> GetPublicReviewsAsync(PublicFeedbackQuery query, CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPublicPageSize);
        var skip = (page - 1) * pageSize;
        var q = _repository.PublicQuery();
        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(f => f.CreatedAt)
            .ThenBy(f => f.FeedbackId)
            .Skip(skip)
            .Take(pageSize)
            .Select(f => new
            {
                f.UserId,
                Feedback = new PublicSystemFeedbackDto
                {
                    FeedbackId = f.FeedbackId,
                    OverallRating = f.Rating,
                    UiUxRating = f.UiUxRating,
                    UsabilityRating = f.UsabilityRating,
                    FeatureQualityRating = f.FeatureQualityRating,
                    Comment = f.Content,
                    Category = f.Category,
                    CreatedAt = f.CreatedAt
                }
            })
            .ToListAsync(ct);
        await ApplyPublicAuthorNamesAsync(
            rows.Select(row => (row.UserId, row.Feedback)).ToList(),
            ct);
        var items = rows.Select(row => row.Feedback).ToList();
        return ToPaged(items, page, pageSize, total);
    }

    public async Task<SystemFeedbackSubmissionStatusDto> GetSubmissionStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var feedbackCount = await _repository.CountByUserIdAsync(userId, ct);
        var latestFeedback = await _repository.GetLatestByUserIdAsync(userId, ct);
        return new SystemFeedbackSubmissionStatusDto
        {
            HasSubmittedSystemFeedback = feedbackCount > 0,
            FeedbackCount = feedbackCount,
            LastSubmittedAt = latestFeedback?.CreatedAt
        };
    }

    public async Task<CreatedSystemFeedbackDto> SubmitAsync(Guid userId, SubmitSystemFeedbackRequest request, string? bearerToken, CancellationToken ct = default)
    {
        ValidateRating(request.OverallRating, nameof(request.OverallRating));
        ValidateRating(request.UiUxRating, nameof(request.UiUxRating));
        ValidateRating(request.UsabilityRating, nameof(request.UsabilityRating));
        ValidateRating(request.FeatureQualityRating, nameof(request.FeatureQualityRating));
        var comment = FeedbackInput.NormalizeOptionalComment(request.Comment, SystemFeedbackRules.MaxCommentLength);

        var eligibility = await _interviewApiClient.GetSystemFeedbackEligibilityAsync(bearerToken, ct);
        if (!eligibility.Eligible)
            throw new FeedbackEligibilityException("User is not eligible to submit system feedback.");

        var now = VietnamTime.Now;
        var feedback = new SystemFeedback
        {
            FeedbackId = Guid.NewGuid(),
            UserId = userId,
            Category = "General",
            Subject = "System Feedback",
            Content = comment ?? string.Empty,
            Rating = (byte)request.OverallRating,
            UiUxRating = (byte)request.UiUxRating,
            UsabilityRating = (byte)request.UsabilityRating,
            FeatureQualityRating = (byte)request.FeatureQualityRating,
            Status = NewStatus,
            ModerationStatus = Approved,
            IsVisible = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.AddAsync(feedback, ct);
        await _repository.SaveChangesAsync(ct);
        return new CreatedSystemFeedbackDto
        {
            FeedbackId = feedback.FeedbackId,
            OverallRating = feedback.Rating!.Value,
            UiUxRating = feedback.UiUxRating!.Value,
            UsabilityRating = feedback.UsabilityRating!.Value,
            FeatureQualityRating = feedback.FeatureQualityRating!.Value,
            Comment = comment,
            ModerationStatus = feedback.ModerationStatus,
            IsVisible = feedback.IsVisible,
            CreatedAt = feedback.CreatedAt
        };
    }

    public async Task<ModerationResponse?> ModerateAsync(Guid feedbackId, Guid adminId, ModerationRequest request, CancellationToken ct = default)
    {
        var status = NormalizeModerationStatus(request.ModerationStatus);
        var feedback = await _repository.GetByIdAsync(feedbackId, tracking: true, ct: ct);
        if (feedback is null) return null;
        var visible = status == Approved && request.IsVisible && !feedback.IsDeleted;
        if (request.IsVisible && status == Approved && feedback.IsDeleted)
            throw new FeedbackValidationException("Deleted feedback cannot become visible.");

        feedback.ModerationStatus = status;
        feedback.IsVisible = visible;
        feedback.ModeratedAt = VietnamTime.Now;
        feedback.ModeratedBy = adminId;
        feedback.UpdatedAt = VietnamTime.Now;
        await _repository.SaveChangesAsync(ct);
        return new ModerationResponse
        {
            FeedbackId = feedback.FeedbackId,
            ModerationStatus = feedback.ModerationStatus,
            IsVisible = feedback.IsVisible,
            ModeratedAt = feedback.ModeratedAt,
            ModeratedBy = feedback.ModeratedBy
        };
    }

    public async Task<bool> SoftDeleteAsync(Guid id)
    {
        var f = await _repository.GetByIdAsync(id, tracking: true);
        if (f is null || f.IsDeleted) return false;
        f.IsDeleted = true;
        f.IsVisible = false;
        f.DeletedAt = VietnamTime.Now;
        f.UpdatedAt = VietnamTime.Now;
        await _repository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreAsync(Guid id)
    {
        var f = await _repository.GetByIdAsync(id, tracking: true);
        if (f is null || !f.IsDeleted) return false;

        f.IsDeleted = false;
        f.DeletedAt = null;
        f.UpdatedAt = VietnamTime.Now;
        await _repository.SaveChangesAsync();
        return true;
    }

    public async Task<FeedbackReplyDto?> ReplyAsync(Guid feedbackId, Guid repliedBy, string replyContent)
    {
        var feedback = await _repository.GetByIdAsync(feedbackId, tracking: true);
        if (feedback is null || feedback.IsDeleted) return null;
        var reply = new FeedbackReply { FeedbackId = feedbackId, RepliedBy = repliedBy, ReplyContent = replyContent };
        await _repository.AddReplyAsync(reply);
        feedback.Status = RepliedStatus;
        feedback.UpdatedAt = VietnamTime.Now;
        await _repository.SaveChangesAsync();
        return MapReply(reply);
    }

    public async Task<FeedbackReplyDto?> UpdateReplyAsync(Guid feedbackId, Guid replyId, string replyContent)
    {
        var feedback = await _repository.GetByIdAsync(feedbackId, includeReplies: true, tracking: true);
        if (feedback is null || feedback.IsDeleted) return null;
        var reply = feedback.FeedbackReplies.FirstOrDefault(r => r.ReplyId == replyId);
        if (reply is null) return null;
        reply.ReplyContent = replyContent;
        feedback.UpdatedAt = VietnamTime.Now;
        await _repository.SaveChangesAsync();
        return MapReply(reply);
    }

    public async Task<bool> DeleteReplyAsync(Guid feedbackId, Guid replyId)
    {
        var feedback = await _repository.GetByIdAsync(feedbackId, includeReplies: true, tracking: true);
        if (feedback is null || feedback.IsDeleted) return false;
        var reply = feedback.FeedbackReplies.FirstOrDefault(r => r.ReplyId == replyId);
        if (reply is null) return false;
        _repository.RemoveReply(reply);
        if (feedback.FeedbackReplies.Count(r => r.ReplyId != replyId) == 0 && feedback.Status == RepliedStatus)
            feedback.Status = NewStatus;
        feedback.UpdatedAt = VietnamTime.Now;
        await _repository.SaveChangesAsync();
        return true;
    }

    public async Task<SystemFeedbackListItemDto?> GetDetailAsync(Guid id)
    {
        var f = await _repository.GetByIdAsync(id, includeReplies: true, tracking: false);
        if (f is null) return null;
        return new SystemFeedbackListItemDto
        {
            Id = f.FeedbackId,
            UserId = f.UserId,
            Category = f.Category,
            Subject = f.Subject,
            Content = f.Content,
            Rating = f.Rating,
                UiUxRating = f.UiUxRating,
                UsabilityRating = f.UsabilityRating,
                FeatureQualityRating = f.FeatureQualityRating,
                Status = f.Status,
                ModerationStatus = f.ModerationStatus,
                IsVisible = f.IsVisible,
                ModeratedAt = f.ModeratedAt,
                ModeratedBy = f.ModeratedBy,
            ReplyCount = f.FeedbackReplies.Count,
            IsDeleted = f.IsDeleted,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt,
            Replies = f.FeedbackReplies.OrderBy(r => r.CreatedAt).Select(MapReply).ToList()
        };
    }

    private async Task ResolveUsersAsync(List<SystemFeedbackListItemDto> items)
    {
        var accountIds = items.Select(i => i.UserId).Concat(items.SelectMany(i => i.Replies).Select(r => r.RepliedBy)).Where(id => id != Guid.Empty).Distinct().ToList();
        if (accountIds.Count == 0) return;
        var userIds = items.Select(i => i.UserId).Where(id => id != Guid.Empty).Distinct().ToList();
        var namesTask = _userApiClient.GetFullNamesAsync(accountIds);
        var emailsTask = _authApiClient.GetEmailsAsync(userIds);
        await Task.WhenAll(namesTask, emailsTask);
        foreach (var item in items)
        {
            if (namesTask.Result.TryGetValue(item.UserId, out var fn)) item.UserFullName = fn;
            if (emailsTask.Result.TryGetValue(item.UserId, out var em)) item.UserEmail = em;
            foreach (var reply in item.Replies)
                if (namesTask.Result.TryGetValue(reply.RepliedBy, out var rn)) reply.RepliedByName = rn;
        }
    }

    private async Task ApplyPublicAuthorNamesAsync(
        IReadOnlyCollection<(Guid UserId, PublicSystemFeedbackDto Feedback)> rows,
        CancellationToken ct)
    {
        var names = await PublicAuthorDisplayName.ResolveBatchAsync(
            _userApiClient,
            rows.Select(row => row.UserId),
            ct);

        foreach (var row in rows)
        {
            row.Feedback.AuthorDisplayName = names.GetValueOrDefault(
                row.UserId,
                PublicAuthorDisplayName.Fallback);
        }
    }

    private static FeedbackReplyDto MapReply(FeedbackReply r) => new()
    {
        ReplyId = r.ReplyId,
        FeedbackId = r.FeedbackId,
        RepliedBy = r.RepliedBy,
        ReplyContent = r.ReplyContent,
        CreatedAt = r.CreatedAt
    };

    private static void ValidateRating(int rating, string field)
    {
        if (rating < 1 || rating > 5) throw new FeedbackValidationException($"{field} must be between 1 and 5.");
    }


    private static string NormalizeModerationStatus(string status) => status?.Trim() switch
    {
        Pending => Pending,
        Approved => Approved,
        Rejected => Rejected,
        _ => throw new FeedbackValidationException("ModerationStatus must be Pending, Approved, or Rejected.")
    };

    private static PagedResult<T> ToPaged<T>(IReadOnlyList<T> items, int page, int pageSize, int total) => new()
    {
        Items = items,
        Page = page,
        PageSize = pageSize,
        TotalItems = total,
        TotalPages = (int)Math.Ceiling(total / (double)pageSize)
    };
}

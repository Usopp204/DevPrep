using FeedbackAPI.Clients;
using FeedbackAPI.DTOs;
using FeedbackAPI.Helpers;
using FeedbackAPI.Models;
using FeedbackAPI.Repositories.Interfaces;
using FeedbackAPI.Services.Interfaces;
using FeedbackAPI.Validation;

using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Services;

public class AiFeedbackService : IAiFeedbackService
{
    private const int MaxAdminPageSize = 100;
    private const int MaxPublicPageSize = 50;
    private const string ApprovedModerationStatus = "Approved";


    private readonly IAiFeedbackRepository _repository;
    private readonly IUserApiClient _userApiClient;
    private readonly IAuthApiClient _authApiClient;
    private readonly IInterviewApiClient _interviewApiClient;

    public AiFeedbackService(
        IAiFeedbackRepository repository,
        IUserApiClient userApiClient,
        IAuthApiClient authApiClient,
        IInterviewApiClient interviewApiClient)
    {
        _repository = repository;
        _userApiClient = userApiClient;
        _authApiClient = authApiClient;
        _interviewApiClient = interviewApiClient;
    }

    public async Task<PagedResult<AiFeedbackListItemDto>> GetListAsync(AiFeedbackQuery query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, MaxAdminPageSize);
        var skip = (page - 1) * pageSize;

        // Admin can request only deleted records, all records, or active records.
        var q = query.OnlyDeleted
            ? _repository.QueryIncludingDeleted().Where(f => f.IsDeleted)
            : query.IncludeDeleted ? _repository.QueryIncludingDeleted() : _repository.Query();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            var nameIds = await _userApiClient.SearchIdsByNameAsync(kw);
            var emailIds = await _authApiClient.SearchIdsByEmailAsync(kw);
            var matchIds = nameIds.Concat(emailIds).Distinct().ToList();
            q = q.Where(f => (f.Content != null && f.Content.Contains(kw)) || matchIds.Contains(f.UserId));
        }
        if (query.Rating.HasValue) q = q.Where(f => f.Rating == query.Rating.Value);
        if (query.IsHelpful.HasValue) q = q.Where(f => f.IsHelpful == query.IsHelpful.Value);
        if (query.UserId.HasValue) q = q.Where(f => f.UserId == query.UserId.Value);
        if (query.SessionId.HasValue) q = q.Where(f => f.SessionId == query.SessionId.Value);
        if (query.DateFrom.HasValue) q = q.Where(f => f.CreatedAt >= query.DateFrom.Value);
        if (query.DateTo.HasValue) q = q.Where(f => f.CreatedAt <= query.DateTo.Value);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(f => f.CreatedAt).Skip(skip).Take(pageSize).Select(f => new AiFeedbackListItemDto
        {
            Id = f.InterviewFeedbackId,
            SessionId = f.SessionId,
            UserId = f.UserId,
            Rating = f.Rating,
            QuestionQualityRating = f.QuestionQualityRating,
            AiAccuracyRating = f.AiAccuracyRating,
            ScoringFairnessRating = f.ScoringFairnessRating,
            FeedbackUsefulnessRating = f.FeedbackUsefulnessRating,
            Content = f.Content,
            IsHelpful = f.IsHelpful,

            IsVisible = f.IsVisible,


            IsDeleted = f.IsDeleted,
            CreatedAt = f.CreatedAt
        }).ToListAsync();

        await ResolveUsersAsync(items);
        return ToPaged(items, page, pageSize, total);
    }

    public async Task<PagedResult<PublicAiFeedbackDto>> GetPublicReviewsAsync(
        PublicFeedbackQuery query,
        Guid? viewerUserId,
        CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPublicPageSize);
        var skip = (page - 1) * pageSize;
        var q = _repository.PublicQuery();
        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(f => f.CreatedAt)
            .ThenBy(f => f.InterviewFeedbackId)
            .Skip(skip)
            .Take(pageSize)
            .Select(f => new
            {
                f.UserId,
                Feedback = new PublicAiFeedbackDto
                {
                    FeedbackId = f.InterviewFeedbackId,
                    OverallRating = f.Rating,
                    QuestionQualityRating = f.QuestionQualityRating,
                    AiAccuracyRating = f.AiAccuracyRating,
                    ScoringFairnessRating = f.ScoringFairnessRating,
                    FeedbackUsefulnessRating = f.FeedbackUsefulnessRating,
                    Comment = f.Content,
                    CreatedAt = f.CreatedAt,
                    IsOwner = viewerUserId.HasValue && f.UserId == viewerUserId.Value
                }
            })
            .ToListAsync(ct);
        await ApplyPublicAuthorNamesAsync(
            rows.Select(row => (row.UserId, row.Feedback)).ToList(),
            ct);
        var items = rows.Select(row => row.Feedback).ToList();
        return ToPaged(items, page, pageSize, total);
    }


    public async Task<MyInterviewFeedbackDto?> GetMyFeedbackAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        if (sessionId == Guid.Empty) throw new FeedbackValidationException("SessionId is required.");
        var feedback = await _repository.GetByUserAndSessionAsync(userId, sessionId, ct);
        if (feedback is null) return null;

        return new MyInterviewFeedbackDto
        {
            FeedbackId = feedback.InterviewFeedbackId,
            SessionId = feedback.SessionId,
            OverallRating = feedback.Rating,
            QuestionQualityRating = feedback.QuestionQualityRating,
            AiAccuracyRating = feedback.AiAccuracyRating,
            ScoringFairnessRating = feedback.ScoringFairnessRating,
            FeedbackUsefulnessRating = feedback.FeedbackUsefulnessRating,
            IsHelpful = feedback.IsHelpful,
            Comment = feedback.Content,

            IsVisible = feedback.IsVisible,
            CreatedAt = feedback.CreatedAt
        };
    }
    public async Task<CreatedInterviewFeedbackDto> SubmitAsync(Guid userId, SubmitInterviewFeedbackRequest request, string? bearerToken, CancellationToken ct = default)
    {
        if (request.SessionId == Guid.Empty) throw new FeedbackValidationException("SessionId is required.");
        ValidateRating(request.OverallRating, nameof(request.OverallRating));
        ValidateRating(request.QuestionQualityRating, nameof(request.QuestionQualityRating));
        ValidateRating(request.AiAccuracyRating, nameof(request.AiAccuracyRating));
        ValidateRating(request.ScoringFairnessRating, nameof(request.ScoringFairnessRating));
        ValidateRating(request.FeedbackUsefulnessRating, nameof(request.FeedbackUsefulnessRating));
        var comment = FeedbackInput.NormalizeOptionalComment(request.Comment, InterviewFeedbackRules.MaxCommentLength);

        var eligibility = await _interviewApiClient.GetSessionFeedbackEligibilityAsync(request.SessionId, bearerToken, ct);
        if (!eligibility.CanSubmitInterviewFeedback)
            throw new FeedbackEligibilityException("Interview session is not eligible for feedback.");


        if (await _repository.ActiveExistsAsync(userId, request.SessionId, ct))
            throw new DuplicateFeedbackException();

        var feedback = new InterviewFeedback
        {
            InterviewFeedbackId = Guid.NewGuid(),
            SessionId = request.SessionId,
            UserId = userId,
            Rating = (byte)request.OverallRating,
            QuestionQualityRating = (byte)request.QuestionQualityRating,
            AiAccuracyRating = (byte)request.AiAccuracyRating,
            ScoringFairnessRating = (byte)request.ScoringFairnessRating,
            FeedbackUsefulnessRating = (byte)request.FeedbackUsefulnessRating,
            IsHelpful = request.IsHelpful,
            Content = comment,
            IsVisible = true,
            ModerationStatus = ApprovedModerationStatus,
            IsDeleted = false,
            CreatedAt = VietnamTime.Now
        };

        if (!await _repository.TryAddActiveAsync(feedback, ct))
            throw new DuplicateFeedbackException();

        return new CreatedInterviewFeedbackDto
        {
            FeedbackId = feedback.InterviewFeedbackId,
            SessionId = feedback.SessionId,
            OverallRating = feedback.Rating,
            QuestionQualityRating = feedback.QuestionQualityRating!.Value,
            AiAccuracyRating = feedback.AiAccuracyRating!.Value,
            ScoringFairnessRating = feedback.ScoringFairnessRating!.Value,
            FeedbackUsefulnessRating = feedback.FeedbackUsefulnessRating!.Value,
            Comment = feedback.Content,

            IsVisible = feedback.IsVisible,
            CreatedAt = feedback.CreatedAt
        };
    }

    public async Task EditAsync(Guid feedbackId, Guid userId, UpdateInterviewFeedbackRequest request, CancellationToken ct = default)
    {
        var feedback = await _repository.GetOwnedActiveByIdAsync(feedbackId, userId, tracking: true, ct);
        if (feedback is null) throw new FeedbackNotFoundException("Interview feedback not found.");

        ValidateRating(request.OverallRating, nameof(request.OverallRating));
        ValidateRating(request.QuestionQualityRating, nameof(request.QuestionQualityRating));
        ValidateRating(request.AiAccuracyRating, nameof(request.AiAccuracyRating));
        ValidateRating(request.ScoringFairnessRating, nameof(request.ScoringFairnessRating));
        ValidateRating(request.FeedbackUsefulnessRating, nameof(request.FeedbackUsefulnessRating));

        feedback.Rating = (byte)request.OverallRating;
        feedback.QuestionQualityRating = (byte)request.QuestionQualityRating;
        feedback.AiAccuracyRating = (byte)request.AiAccuracyRating;
        feedback.ScoringFairnessRating = (byte)request.ScoringFairnessRating;
        feedback.FeedbackUsefulnessRating = (byte)request.FeedbackUsefulnessRating;
        feedback.Content = FeedbackInput.NormalizeOptionalComment(request.Comment, InterviewFeedbackRules.MaxCommentLength);
        feedback.IsVisible = true;
        feedback.ModerationStatus = ApprovedModerationStatus;

        await _repository.SaveChangesAsync(ct);
    }

    public async Task CandidateDeleteAsync(Guid feedbackId, Guid userId, CancellationToken ct = default)
    {
        var feedback = await _repository.GetOwnedActiveByIdAsync(feedbackId, userId, tracking: true, ct);
        if (feedback is null) throw new FeedbackNotFoundException("Interview feedback not found.");

        feedback.IsDeleted = true;
        feedback.IsVisible = false;
        feedback.DeletedAt = VietnamTime.Now;
        await _repository.ResolvePendingReportsAsync(feedback.InterviewFeedbackId, ct);

        await _repository.SaveChangesAsync(ct);
    }

    public async Task<bool> SoftDeleteAsync(Guid id)
    {
        var f = await _repository.GetByIdAsync(id, tracking: true);
        if (f is null || f.IsDeleted) return false;
        f.IsDeleted = true;
        f.IsVisible = false;
        f.DeletedAt = VietnamTime.Now;
        await _repository.SaveChangesAsync();
        return true;
    }

    private async Task ResolveUsersAsync(List<AiFeedbackListItemDto> items)
    {
        var userIds = items.Select(i => i.UserId).Where(id => id != Guid.Empty).Distinct().ToList();
        if (userIds.Count == 0) return;
        var namesTask = _userApiClient.GetFullNamesAsync(userIds);
        var emailsTask = _authApiClient.GetEmailsAsync(userIds);
        await Task.WhenAll(namesTask, emailsTask);
        foreach (var item in items)
        {
            if (namesTask.Result.TryGetValue(item.UserId, out var fullName)) item.UserFullName = fullName;
            if (emailsTask.Result.TryGetValue(item.UserId, out var email)) item.UserEmail = email;
        }
    }

    private async Task ApplyPublicAuthorNamesAsync(
        IReadOnlyCollection<(Guid UserId, PublicAiFeedbackDto Feedback)> rows,
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

    private static void ValidateRating(int rating, string field)
    {
        if (rating < 1 || rating > 5) throw new FeedbackValidationException($"{field} must be between 1 and 5.");
    }


    private static PagedResult<T> ToPaged<T>(IReadOnlyList<T> items, int page, int pageSize, int total) => new()
    {
        Items = items,
        Page = page,
        PageSize = pageSize,
        TotalItems = total,
        TotalPages = (int)Math.Ceiling(total / (double)pageSize)
    };

    public async Task<bool> RestoreAsync(Guid id)
    {
        var outcome = await _repository.RestoreAsync(id);
        if (outcome == FeedbackRestoreOutcome.Conflict)
            throw new FeedbackEligibilityException("Cannot restore: an active interview feedback already exists for this session.");
        return outcome == FeedbackRestoreOutcome.Restored;
    }
}

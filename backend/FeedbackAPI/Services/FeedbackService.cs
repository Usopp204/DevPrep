using FeedbackAPI.DTOs;
using FeedbackAPI.Helpers;
using FeedbackAPI.Models;
using FeedbackAPI.Repositories.Interfaces;
using FeedbackAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Services;

public class FeedbackService : IFeedbackService
{
    private const int MaxPageSize = 100;

    private static readonly string[] AllowedStatuses =
        { "New", "InReview", "Replied", "Resolved", "Closed" };
    private static readonly string[] AllowedCategories =
        { "General", "Bug", "FeatureRequest", "UI", "Performance", "Other" };

    private const string RepliedStatus = "Replied";

    private readonly IFeedbackRepository _repository;

    public FeedbackService(IFeedbackRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<FeedbackListItemDto>> GetListAsync(FeedbackType? type, int page, int pageSize)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var skip = (page - 1) * pageSize;

        List<FeedbackListItemDto> items;
        int total;

        if (type == FeedbackType.System)
        {
            total = await _repository.QuerySystem().CountAsync();
            items = await SystemListQuery()
                .OrderByDescending(x => x.CreatedAt)
                .Skip(skip).Take(pageSize)
                .ToListAsync();
        }
        else if (type == FeedbackType.AI)
        {
            total = await _repository.QueryInterview().CountAsync();
            items = await InterviewListQuery()
                .OrderByDescending(x => x.CreatedAt)
                .Skip(skip).Take(pageSize)
                .ToListAsync();
        }
        else
        {
            // Tất cả: gộp 2 bảng rồi sắp xếp + phân trang trong bộ nhớ
            // (số lượng feedback nhỏ nên chấp nhận được).
            var system = await SystemListQuery().ToListAsync();
            var interview = await InterviewListQuery().ToListAsync();
            var merged = system.Concat(interview)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
            total = merged.Count;
            items = merged.Skip(skip).Take(pageSize).ToList();
        }

        return new PagedResult<FeedbackListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<FeedbackDetailDto?> GetDetailAsync(FeedbackType type, Guid id)
    {
        if (type == FeedbackType.System)
        {
            var feedback = await _repository.GetSystemByIdAsync(id, includeReplies: true);
            return feedback is null ? null : MapSystemDetail(feedback);
        }

        var interview = await _repository.GetInterviewByIdAsync(id);
        return interview is null ? null : MapInterviewDetail(interview);
    }

    public async Task<FeedbackDetailDto?> UpdateAsync(FeedbackType type, Guid id, UpdateFeedbackRequest request)
    {
        if (request.Rating.HasValue && (request.Rating < 1 || request.Rating > 5))
        {
            throw new ArgumentException("Rating must be between 1 and 5.");
        }

        if (type == FeedbackType.System)
        {
            var feedback = await _repository.GetSystemByIdAsync(id, includeReplies: true, tracking: true);
            if (feedback is null) return null;

            if (request.Status is not null)
            {
                if (!AllowedStatuses.Contains(request.Status))
                    throw new ArgumentException($"Invalid status. Allowed: {string.Join(", ", AllowedStatuses)}.");
                feedback.Status = request.Status;
            }
            if (request.Category is not null)
            {
                if (!AllowedCategories.Contains(request.Category))
                    throw new ArgumentException($"Invalid category. Allowed: {string.Join(", ", AllowedCategories)}.");
                feedback.Category = request.Category;
            }
            if (request.Subject is not null) feedback.Subject = request.Subject;
            if (request.Content is not null) feedback.Content = request.Content;
            if (request.Rating.HasValue) feedback.Rating = request.Rating;

            feedback.UpdatedAt = VietnamTime.Now;
            await _repository.SaveChangesAsync();
            return MapSystemDetail(feedback);
        }
        else
        {
            var interview = await _repository.GetInterviewByIdAsync(id, tracking: true);
            if (interview is null) return null;

            if (request.Content is not null) interview.Content = request.Content;
            if (request.Rating.HasValue) interview.Rating = request.Rating.Value;
            if (request.IsHelpful.HasValue) interview.IsHelpful = request.IsHelpful;

            await _repository.SaveChangesAsync();
            return MapInterviewDetail(interview);
        }
    }

    public async Task<bool> SoftDeleteAsync(FeedbackType type, Guid id)
    {
        if (type == FeedbackType.System)
        {
            var feedback = await _repository.GetSystemByIdAsync(id, tracking: true);
            if (feedback is null || feedback.IsDeleted) return false;
            feedback.IsDeleted = true;
            feedback.DeletedAt = VietnamTime.Now;
            feedback.UpdatedAt = VietnamTime.Now;
            await _repository.SaveChangesAsync();
            return true;
        }
        else
        {
            var interview = await _repository.GetInterviewByIdAsync(id, tracking: true);
            if (interview is null || interview.IsDeleted) return false;
            interview.IsDeleted = true;
            interview.DeletedAt = VietnamTime.Now;
            await _repository.SaveChangesAsync();
            return true;
        }
    }

    public async Task<FeedbackReplyDto?> ReplyAsync(Guid feedbackId, Guid repliedBy, string replyContent)
    {
        // Reply chỉ áp dụng cho System feedback (FeedbackReplies FK -> SystemFeedbacks).
        var feedback = await _repository.GetSystemByIdAsync(feedbackId, tracking: true);
        if (feedback is null || feedback.IsDeleted) return null;

        var reply = new FeedbackReply
        {
            FeedbackId = feedbackId,
            RepliedBy = repliedBy,
            ReplyContent = replyContent
        };
        await _repository.AddReplyAsync(reply);

        // Đã trả lời -> chuyển trạng thái.
        feedback.Status = RepliedStatus;
        feedback.UpdatedAt = VietnamTime.Now;

        await _repository.SaveChangesAsync();

        return new FeedbackReplyDto
        {
            ReplyId = reply.ReplyId,
            RepliedBy = reply.RepliedBy,
            ReplyContent = reply.ReplyContent,
            CreatedAt = reply.CreatedAt
        };
    }

    // ---------- Projections / mappers ----------

    private IQueryable<FeedbackListItemDto> SystemListQuery() =>
        _repository.QuerySystem().Select(f => new FeedbackListItemDto
        {
            Id = f.FeedbackId,
            Type = "System",
            UserId = f.UserId,
            Subject = f.Subject,
            Category = f.Category,
            Status = f.Status,
            ReplyCount = f.FeedbackReplies.Count,
            SessionId = null,
            IsHelpful = null,
            Content = f.Content,
            Rating = f.Rating,
            IsDeleted = f.IsDeleted,
            CreatedAt = f.CreatedAt
        });

    private IQueryable<FeedbackListItemDto> InterviewListQuery() =>
        _repository.QueryInterview().Select(f => new FeedbackListItemDto
        {
            Id = f.InterviewFeedbackId,
            Type = "AI",
            UserId = f.UserId,
            Subject = null,
            Category = null,
            Status = null,
            ReplyCount = 0,
            SessionId = f.SessionId,
            IsHelpful = f.IsHelpful,
            Content = f.Content,
            Rating = f.Rating,
            IsDeleted = f.IsDeleted,
            CreatedAt = f.CreatedAt
        });

    private static FeedbackDetailDto MapSystemDetail(SystemFeedback f) => new()
    {
        Id = f.FeedbackId,
        Type = "System",
        UserId = f.UserId,
        Subject = f.Subject,
        Category = f.Category,
        Status = f.Status,
        SessionId = null,
        IsHelpful = null,
        Content = f.Content,
        Rating = f.Rating,
        IsDeleted = f.IsDeleted,
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt,
        Replies = f.FeedbackReplies
            .OrderBy(r => r.CreatedAt)
            .Select(r => new FeedbackReplyDto
            {
                ReplyId = r.ReplyId,
                RepliedBy = r.RepliedBy,
                ReplyContent = r.ReplyContent,
                CreatedAt = r.CreatedAt
            })
            .ToList()
    };

    private static FeedbackDetailDto MapInterviewDetail(InterviewFeedback f) => new()
    {
        Id = f.InterviewFeedbackId,
        Type = "AI",
        UserId = f.UserId,
        Subject = null,
        Category = null,
        Status = null,
        SessionId = f.SessionId,
        IsHelpful = f.IsHelpful,
        Content = f.Content,
        Rating = f.Rating,
        IsDeleted = f.IsDeleted,
        CreatedAt = f.CreatedAt,
        UpdatedAt = null,
        Replies = new List<FeedbackReplyDto>()
    };
}

using FeedbackAPI.DTOs;
using FeedbackAPI.Helpers;
using FeedbackAPI.Models;
using FeedbackAPI.Repositories.Interfaces;
using FeedbackAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Services;

public class SystemFeedbackService : ISystemFeedbackService
{
    private const int MaxPageSize = 100;

    private const string NewStatus = "New";
    private const string RepliedStatus = "Replied";

    private readonly ISystemFeedbackRepository _repository;

    public SystemFeedbackService(ISystemFeedbackRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<SystemFeedbackListItemDto>> GetListAsync(SystemFeedbackQuery query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var skip = (page - 1) * pageSize;

        var q = _repository.Query();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            q = q.Where(f => (f.Subject != null && f.Subject.Contains(kw)) || f.Content.Contains(kw));
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
                Status = f.Status,
                ReplyCount = f.FeedbackReplies.Count,
                IsDeleted = f.IsDeleted,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt,
                Replies = f.FeedbackReplies
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new FeedbackReplyDto
                    {
                        ReplyId = r.ReplyId,
                        FeedbackId = r.FeedbackId,
                        RepliedBy = r.RepliedBy,
                        ReplyContent = r.ReplyContent,
                        CreatedAt = r.CreatedAt
                    })
                    .ToList()
            })
            .ToListAsync();

        return new PagedResult<SystemFeedbackListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<bool> SoftDeleteAsync(Guid id)
    {
        var f = await _repository.GetByIdAsync(id, tracking: true);
        if (f is null || f.IsDeleted) return false;

        f.IsDeleted = true;
        f.DeletedAt = VietnamTime.Now;
        f.UpdatedAt = VietnamTime.Now;
        await _repository.SaveChangesAsync();
        return true;
    }

    public async Task<FeedbackReplyDto?> ReplyAsync(Guid feedbackId, Guid repliedBy, string replyContent)
    {
        var feedback = await _repository.GetByIdAsync(feedbackId, tracking: true);
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

        // Không còn trả lời nào -> đưa trạng thái về "New" cho nhất quán
        // (không còn endpoint cập nhật trạng thái thủ công).
        if (feedback.FeedbackReplies.Count(r => r.ReplyId != replyId) == 0 && feedback.Status == RepliedStatus)
        {
            feedback.Status = NewStatus;
        }
        feedback.UpdatedAt = VietnamTime.Now;

        await _repository.SaveChangesAsync();
        return true;
    }

    // ---------- Mappers ----------

    private static FeedbackReplyDto MapReply(FeedbackReply r) => new()
    {
        ReplyId = r.ReplyId,
        FeedbackId = r.FeedbackId,
        RepliedBy = r.RepliedBy,
        ReplyContent = r.ReplyContent,
        CreatedAt = r.CreatedAt
    };
}

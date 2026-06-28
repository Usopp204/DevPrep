using FeedbackAPI.DTOs;
using FeedbackAPI.Helpers;
using FeedbackAPI.Repositories.Interfaces;
using FeedbackAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Services;

public class AiFeedbackService : IAiFeedbackService
{
    private const int MaxPageSize = 100;

    private readonly IAiFeedbackRepository _repository;

    public AiFeedbackService(IAiFeedbackRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<AiFeedbackListItemDto>> GetListAsync(AiFeedbackQuery query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var skip = (page - 1) * pageSize;

        var q = _repository.Query();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            q = q.Where(f => f.Content != null && f.Content.Contains(kw));
        }
        if (query.Rating.HasValue) q = q.Where(f => f.Rating == query.Rating.Value);
        if (query.IsHelpful.HasValue) q = q.Where(f => f.IsHelpful == query.IsHelpful.Value);
        if (query.UserId.HasValue) q = q.Where(f => f.UserId == query.UserId.Value);
        if (query.SessionId.HasValue) q = q.Where(f => f.SessionId == query.SessionId.Value);
        if (query.DateFrom.HasValue) q = q.Where(f => f.CreatedAt >= query.DateFrom.Value);
        if (query.DateTo.HasValue) q = q.Where(f => f.CreatedAt <= query.DateTo.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(f => f.CreatedAt)
            .Skip(skip).Take(pageSize)
            .Select(f => new AiFeedbackListItemDto
            {
                Id = f.InterviewFeedbackId,
                SessionId = f.SessionId,
                UserId = f.UserId,
                Rating = f.Rating,
                Content = f.Content,
                IsHelpful = f.IsHelpful,
                IsDeleted = f.IsDeleted,
                CreatedAt = f.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<AiFeedbackListItemDto>
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
        await _repository.SaveChangesAsync();
        return true;
    }
}

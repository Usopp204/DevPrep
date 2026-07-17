using FeedbackAPI.Clients;
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
    private readonly IUserApiClient _userApiClient;
    private readonly IAuthApiClient _authApiClient;

    public AiFeedbackService(IAiFeedbackRepository repository, IUserApiClient userApiClient, IAuthApiClient authApiClient)
    {
        _repository = repository;
        _userApiClient = userApiClient;
        _authApiClient = authApiClient;
    }

    public async Task<PagedResult<AiFeedbackListItemDto>> GetListAsync(AiFeedbackQuery query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
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
            // Search cả nội dung + tên (UserAPI) + email (AuthAPI). matchIds rỗng -> chỉ khớp Content.
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

        // Resolve Tên (UserAPI) + Email (AuthAPI) theo UserId — best-effort, lỗi thì để null.
        var userIds = items.Select(i => i.UserId).Where(id => id != Guid.Empty).Distinct().ToList();
        if (userIds.Count > 0)
        {
            var namesTask = _userApiClient.GetFullNamesAsync(userIds);
            var emailsTask = _authApiClient.GetEmailsAsync(userIds);
            await Task.WhenAll(namesTask, emailsTask);
            var names = namesTask.Result;
            var emails = emailsTask.Result;

            foreach (var item in items)
            {
                if (names.TryGetValue(item.UserId, out var fullName)) item.UserFullName = fullName;
                if (emails.TryGetValue(item.UserId, out var email)) item.UserEmail = email;
            }
        }

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

    public async Task<bool> RestoreAsync(Guid id)
    {
        var f = await _repository.GetByIdAsync(id, tracking: true);
        if (f is null || !f.IsDeleted) return false;

        f.IsDeleted = false;
        f.DeletedAt = null;
        await _repository.SaveChangesAsync();
        return true;
    }
}

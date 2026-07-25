using InterviewAPI.Clients;
using InterviewAPI.DTOs;
using InterviewAPI.Helpers;
using InterviewAPI.Models;
using InterviewAPI.Repositories.Interfaces;
using InterviewAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Services;

public class InterviewService : IInterviewService
{
    private const int MaxPageSize = 100;

    private readonly IInterviewRepository _repository;
    private readonly IAuthApiClient _authClient;
    private readonly IUserApiClient _userClient;

    public InterviewService(
        IInterviewRepository repository,
        IAuthApiClient authClient,
        IUserApiClient userClient)
    {
        _repository = repository;
        _authClient = authClient;
        _userClient = userClient;
    }

    public async Task<PagedResult<InterviewListItemDto>> GetListAsync(InterviewQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var skip = (page - 1) * pageSize;

        // OnlyDeleted = true  -> CHỈ phiên đã xoá mềm (trang "Show deleted" của admin).
        // IncludeDeleted = true -> kèm cả phiên đã xoá mềm (dashboard đếm tổng).
        // mặc định -> chỉ phiên chưa xoá.
        var q = query.OnlyDeleted
            ? _repository.QueryIncludingDeleted().Where(s => s.IsDeleted)
            : query.IncludeDeleted ? _repository.QueryIncludingDeleted() : _repository.Query();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            // Search theo tên (UserAPI) + email (AuthAPI). Cột Title đã bỏ khỏi schema.
            var nameIds = await _userClient.SearchIdsByNameAsync(kw, cancellationToken);
            var emailIds = await _authClient.SearchIdsByEmailAsync(kw, cancellationToken);
            var matchIds = nameIds.Concat(emailIds).Distinct().ToList();
            q = q.Where(s => matchIds.Contains(s.UserId));
        }
        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(s => s.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.RoomStatus)) q = q.Where(s => s.InterviewRoom != null && s.InterviewRoom.Status == query.RoomStatus);
        if (!string.IsNullOrWhiteSpace(query.Level)) q = q.Where(s => s.Level == query.Level);
        if (!string.IsNullOrWhiteSpace(query.InteractionMode)) q = q.Where(s => s.InteractionMode == query.InteractionMode);
        if (!string.IsNullOrWhiteSpace(query.QuestionType)) q = q.Where(s => s.QuestionType == query.QuestionType);
        if (query.UserId.HasValue) q = q.Where(s => s.UserId == query.UserId.Value);
        if (query.DateFrom.HasValue) q = q.Where(s => s.CreatedAt >= query.DateFrom.Value);
        if (query.DateTo.HasValue) q = q.Where(s => s.CreatedAt <= query.DateTo.Value);

        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.SessionId)   // tie-breaker: paging ổn định khi CreatedAt trùng
            .Skip(skip).Take(pageSize)
            .Select(s => new InterviewListItemDto
            {
                SessionId = s.SessionId,
                UserId = s.UserId,
                Level = s.Level,
                InteractionMode = s.InteractionMode,
                QuestionType = s.QuestionType,
                Status = s.Status,
                DurationMinutes = s.DurationMinutes,
                TotalQuestions = s.TotalQuestions,
                AnsweredQuestions = s.AnsweredQuestions,
                AvgResponseTimeMs = s.AvgResponseTimeMs,
                CreditCost = s.CreditCost,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                IsDeleted = s.IsDeleted,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Resolve danh tính người dùng cho các phiên trong trang hiện tại (best-effort).
        var userIds = items.Select(i => i.UserId).Distinct().ToList();
        var (emails, fullNames) = await ResolveUsersAsync(userIds, cancellationToken);
        foreach (var item in items)
        {
            item.UserEmail = emails.TryGetValue(item.UserId, out var e) ? e : null;
            item.UserFullName = fullNames.TryGetValue(item.UserId, out var n) ? n : null;
        }

        return new PagedResult<InterviewListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<PagedResult<ManagerInterviewSessionListItemDto>> GetManagerSessionListAsync(InterviewQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var skip = (page - 1) * pageSize;

        var q = _repository.Query()
            .Where(s => s.InterviewRoom != null &&
                (s.InterviewRoom.Status == "Active" || s.InterviewRoom.Status == "Closed"));

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            var nameIds = await _userClient.SearchIdsByNameAsync(kw, cancellationToken);
            var emailIds = await _authClient.SearchIdsByEmailAsync(kw, cancellationToken);
            var matchIds = nameIds.Concat(emailIds).Distinct().ToList();
            q = q.Where(s =>
                (s.Position != null && s.Position.Contains(kw)) ||
                (s.InterviewRoom != null && s.InterviewRoom.ChannelName.Contains(kw)) ||
                matchIds.Contains(s.UserId));
        }

        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(s => s.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.RoomStatus)) q = q.Where(s => s.InterviewRoom != null && s.InterviewRoom.Status == query.RoomStatus);
        if (!string.IsNullOrWhiteSpace(query.Level)) q = q.Where(s => s.Level == query.Level);
        if (!string.IsNullOrWhiteSpace(query.InteractionMode)) q = q.Where(s => s.InteractionMode == query.InteractionMode);
        if (!string.IsNullOrWhiteSpace(query.QuestionType)) q = q.Where(s => s.QuestionType == query.QuestionType);
        if (query.UserId.HasValue) q = q.Where(s => s.UserId == query.UserId.Value);
        if (query.DateFrom.HasValue) q = q.Where(s => s.CreatedAt >= query.DateFrom.Value);
        if (query.DateTo.HasValue) q = q.Where(s => s.CreatedAt <= query.DateTo.Value);

        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.SessionId)
            .Skip(skip).Take(pageSize)
            .Select(s => new ManagerInterviewSessionListItemDto
            {
                SessionId = s.SessionId,
                RoomId = s.InterviewRoom == null ? null : s.InterviewRoom.RoomId,
                ChannelName = s.InterviewRoom == null ? null : s.InterviewRoom.ChannelName,
                RoomStatus = s.InterviewRoom == null ? null : s.InterviewRoom.Status,
                UserId = s.UserId,
                // Cột Title đã bỏ — trang Manager hiển thị Position làm tiêu đề.
                Title = s.Position,
                Level = s.Level,
                InteractionMode = s.InteractionMode,
                QuestionType = s.QuestionType,
                Status = s.Status,
                DurationMinutes = s.DurationMinutes,
                TotalQuestions = s.TotalQuestions,
                AnsweredQuestions = s.AnsweredQuestions,
                AvgResponseTimeMs = s.AvgResponseTimeMs,
                CreditCost = s.CreditCost,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                InitializedAt = s.InterviewRoom == null ? null : s.InterviewRoom.InitializedAt,
                ClosedAt = s.InterviewRoom == null ? null : s.InterviewRoom.ClosedAt,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var userIds = items.Select(i => i.UserId).Distinct().ToList();
        var (emails, fullNames) = await ResolveUsersAsync(userIds, cancellationToken);
        foreach (var item in items)
        {
            item.UserEmail = emails.TryGetValue(item.UserId, out var e) ? e : null;
            item.UserFullName = fullNames.TryGetValue(item.UserId, out var n) ? n : null;
        }

        return new PagedResult<ManagerInterviewSessionListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<InterviewDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetDetailAsync(id, cancellationToken);
        if (s is null) return null;

        var dto = new InterviewDetailDto
        {
            SessionId = s.SessionId,
            UserId = s.UserId,
            CvId = s.CvId,
            JdId = s.JdId,
            Level = s.Level,
            InteractionMode = s.InteractionMode,
            QuestionType = s.QuestionType,
            Status = s.Status,
            DurationMinutes = s.DurationMinutes,
            TotalQuestions = s.TotalQuestions,
            AnsweredQuestions = s.AnsweredQuestions,
            AvgResponseTimeMs = s.AvgResponseTimeMs,
            CreditCost = s.CreditCost,
            StartedAt = s.StartedAt,
            EndedAt = s.EndedAt,
            IsDeleted = s.IsDeleted,
            DeletedAt = s.DeletedAt,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            Questions = s.SessionQuestions
                .OrderBy(q => q.OrderIndex)
                .Select(q => new SessionQuestionDto
                {
                    SessionQuestionId = q.SessionQuestionId,
                    OrderIndex = q.OrderIndex,
                    Content = q.Content,
                    IsAiGenerated = q.IsAiGenerated,
                    ContextSource = q.ContextSource,
                    AskedAt = q.AskedAt,
                    Messages = q.ChatMessages
                        .OrderBy(m => m.SentAt)
                        .Select(m => new ChatMessageDto
                        {
                            MessageId = m.MessageId,
                            SenderType = m.SenderType,
                            MessageType = m.MessageType,
                            Content = m.Content,
                            AudioUrl = m.AudioUrl,
                            ResponseTimeMs = m.ResponseTimeMs,
                            SentAt = m.SentAt
                        })
                        .ToList()
                })
                .ToList()
        };

        var (emails, fullNames) = await ResolveUsersAsync(new[] { s.UserId }, cancellationToken);
        dto.UserEmail = emails.TryGetValue(s.UserId, out var e) ? e : null;
        dto.UserFullName = fullNames.TryGetValue(s.UserId, out var n) ? n : null;

        return dto;
    }

    public async Task<ManagerInterviewSessionLogDto?> GetManagerSessionLogAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetDetailAsync(id, cancellationToken);
        if (s is null) return null;

        var dto = new ManagerInterviewSessionLogDto
        {
            SessionId = s.SessionId,
            RoomId = s.InterviewRoom?.RoomId,
            ChannelName = s.InterviewRoom?.ChannelName,
            RoomStatus = s.InterviewRoom?.Status,
            UserId = s.UserId,
            CvId = s.CvId,
            JdId = s.JdId,
            // Cột Title đã bỏ — trang Manager hiển thị Position làm tiêu đề.
            Title = s.Position,
            Level = s.Level,
            InteractionMode = s.InteractionMode,
            QuestionType = s.QuestionType,
            Status = s.Status,
            DurationMinutes = s.DurationMinutes,
            TotalQuestions = s.TotalQuestions,
            AnsweredQuestions = s.AnsweredQuestions,
            AvgResponseTimeMs = s.AvgResponseTimeMs,
            CreditCost = s.CreditCost,
            StartedAt = s.StartedAt,
            EndedAt = s.EndedAt,
            InitializedAt = s.InterviewRoom?.InitializedAt,
            ClosedAt = s.InterviewRoom?.ClosedAt,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            Questions = s.SessionQuestions
                .OrderBy(q => q.OrderIndex)
                .Select(q => new SessionQuestionDto
                {
                    SessionQuestionId = q.SessionQuestionId,
                    OrderIndex = q.OrderIndex,
                    Content = q.Content,
                    IsAiGenerated = q.IsAiGenerated,
                    ContextSource = q.ContextSource,
                    AskedAt = q.AskedAt,
                    Messages = q.ChatMessages
                        .OrderBy(m => m.SentAt)
                        .Select(m => new ChatMessageDto
                        {
                            MessageId = m.MessageId,
                            SenderType = m.SenderType,
                            MessageType = m.MessageType,
                            Content = m.Content,
                            AudioUrl = m.AudioUrl,
                            ResponseTimeMs = m.ResponseTimeMs,
                            SentAt = m.SentAt
                        })
                        .ToList()
                })
                .ToList()
        };

        var (emails, fullNames) = await ResolveUsersAsync(new[] { s.UserId }, cancellationToken);
        dto.UserEmail = emails.TryGetValue(s.UserId, out var e) ? e : null;
        dto.UserFullName = fullNames.TryGetValue(s.UserId, out var n) ? n : null;

        return dto;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetByIdAsync(id, tracking: true, cancellationToken);
        if (s is null || s.IsDeleted) return false;

        s.IsDeleted = true;
        s.DeletedAt = VietnamTime.Now;
        s.UpdatedAt = VietnamTime.Now;
        await _repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetByIdAsync(id, tracking: true, cancellationToken);
        if (s is null || !s.IsDeleted) return false;

        s.IsDeleted = false;
        s.DeletedAt = null;
        s.UpdatedAt = VietnamTime.Now;
        await _repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Gọi song song 2 service nội bộ để resolve email + fullname (best-effort, lỗi -> dictionary rỗng).
    private async Task<(IReadOnlyDictionary<Guid, string> Emails, IReadOnlyDictionary<Guid, string?> FullNames)>
        ResolveUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
            return (new Dictionary<Guid, string>(), new Dictionary<Guid, string?>());

        // Cả 2 task khởi chạy ngay (đã in-flight) -> await tuần tự vẫn chạy song song.
        var emailsTask = _authClient.GetEmailsAsync(userIds, cancellationToken);
        var namesTask = _userClient.GetFullNamesAsync(userIds, cancellationToken);

        var emails = await emailsTask;
        var fullNames = await namesTask;
        return (emails, fullNames);
    }
}

using InterviewAPI.DTOs.Session;
using InterviewAPI.Helpers;
using InterviewAPI.Models;
using InterviewAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly InterviewDbContext _db;
    private readonly ILogger<SessionRepository> _logger;

    public SessionRepository(InterviewDbContext db, ILogger<SessionRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Lấy phiên theo id, lọc bỏ đã xoá mềm, tracking để có thể cập nhật.
    public Task<InterviewSession?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.InterviewSessions
            .Where(s => s.SessionId == id && !s.IsDeleted)
            .FirstOrDefaultAsync(ct);

    // Lấy phiên kèm danh sách câu hỏi đã sắp xếp theo OrderIndex (tracking, dùng cho generate/answer).
    public Task<InterviewSession?> GetWithQuestionsAsync(Guid sessionId, CancellationToken ct) =>
        _db.InterviewSessions
            .Include(s => s.SessionQuestions.OrderBy(q => q.OrderIndex))
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && !s.IsDeleted, ct);

    // Lấy phiên kèm toàn bộ lịch sử chat (dùng cho resume — tái tạo lại trạng thái màn hình).
    public Task<InterviewSession?> GetWithFullHistoryAsync(Guid sessionId, CancellationToken ct) =>
        _db.InterviewSessions
            .Include(s => s.SessionQuestions.OrderBy(q => q.OrderIndex))
                .ThenInclude(q => q.ChatMessages.OrderBy(m => m.SentAt))
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && !s.IsDeleted, ct);

    public async Task<InterviewSession> CreateAsync(InterviewSession session, CancellationToken ct)
    {
        _db.InterviewSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session;
    }

    public async Task AddSessionQuestionsAsync(List<SessionQuestion> questions, CancellationToken ct)
    {
        _db.SessionQuestions.AddRange(questions);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<InterviewRoom> CreateRoomAsync(InterviewRoom room, CancellationToken ct)
    {
        _db.InterviewRooms.Add(room);
        await _db.SaveChangesAsync(ct);
        return room;
    }

    public async Task<ChatMessage> AddChatMessageAsync(ChatMessage message, CancellationToken ct)
    {
        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync(ct);
        return message;
    }

    // Lấy câu hỏi theo id, kèm session (để service kiểm tra SessionId khớp với route).
    public Task<SessionQuestion?> GetQuestionByIdAsync(Guid sessionQuestionId, CancellationToken ct) =>
        _db.SessionQuestions
            .Include(q => q.Session)
            .FirstOrDefaultAsync(q => q.SessionQuestionId == sessionQuestionId, ct);

    public Task<List<ChatMessage>> GetMessagesByQuestionAsync(Guid sessionQuestionId, CancellationToken ct) =>
        _db.ChatMessages
            .Where(m => m.SessionQuestionId == sessionQuestionId)
            .OrderBy(m => m.SentAt)
            .ToListAsync(ct);

    public Task<bool> HasUserAnswerAsync(Guid sessionQuestionId, CancellationToken ct) =>
        _db.ChatMessages.AnyAsync(m => m.SessionQuestionId == sessionQuestionId
                                     && m.SenderType == "User"
                                     && m.MessageType == "Answer", ct);

    // Câu hỏi đầu tiên sau afterOrderIndex chưa có User Answer nào — dùng để xác định câu tiếp theo.
    public Task<SessionQuestion?> GetNextUnansweredQuestionAsync(
        Guid sessionId, int afterOrderIndex, CancellationToken ct) =>
        _db.SessionQuestions
            .Where(q => q.SessionId == sessionId
                     && q.OrderIndex > afterOrderIndex
                     && !_db.ChatMessages.Any(m => m.SessionQuestionId == q.SessionQuestionId
                                               && m.SenderType == "User"
                                               && m.MessageType == "Answer"))
            .OrderBy(q => q.OrderIndex)
            .FirstOrDefaultAsync(ct);

    public Task<bool> HasSubstantiveAnswerAsync(Guid sessionId, CancellationToken ct) =>
        _db.ChatMessages.AnyAsync(m =>
            m.SessionQuestion.SessionId == sessionId &&
            m.SenderType == "User" &&
            m.MessageType == "Answer" &&
            m.Content != null &&
            m.Content.Trim() != string.Empty &&
            (m.AnswerReason == null ||
             (m.AnswerReason != "TimedOut" && m.AnswerReason != "Skipped")),
            ct);

    // Lấy tất cả phiên của một user (không bao gồm đã xoá mềm), mới nhất trước.
    public Task<List<InterviewSession>> GetByUserAsync(Guid userId, CancellationToken ct) =>
        _db.InterviewSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);


    public async Task<(IReadOnlyList<InterviewSession> Items, int TotalItems, IReadOnlyList<string> PositionOptions)> GetHistoryAsync(
        Guid userId,
        SessionHistoryQuery query,
        CancellationToken ct)
    {
        var normalized = SessionHistoryQueryNormalizer.Normalize(query);

        var ownedFinalSessions = _db.InterviewSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .Where(s => s.Status == "Completed" || s.Status == "Cancelled" || s.Status == "Expired");

        var rawPositionOptions = await ownedFinalSessions
            .Where(s => s.Position != null && s.Position.Trim() != string.Empty)
            .Select(s => s.Position!.Trim())
            .ToListAsync(ct);

        var positionOptions = rawPositionOptions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(position => position, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sessions = ownedFinalSessions;

        if (normalized.Status is not null)
            sessions = sessions.Where(s => s.Status == normalized.Status);

        if (normalized.DateFrom.HasValue)
        {
            var start = normalized.DateFrom.Value.ToDateTime(TimeOnly.MinValue);
            sessions = sessions.Where(s => (s.StartedAt ?? s.CreatedAt) >= start);
        }

        if (normalized.DateTo.HasValue)
        {
            var endExclusive = normalized.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            sessions = sessions.Where(s => (s.StartedAt ?? s.CreatedAt) < endExclusive);
        }

        if (normalized.Position is not null)
        {
            var position = normalized.Position.ToLower();
            sessions = sessions.Where(s => s.Position != null && s.Position.ToLower() == position);
        }

        if (normalized.Keyword is not null)
        {
            var keyword = normalized.Keyword.ToLower();
            // Title column was removed from the schema — search by Position only.
            sessions = sessions.Where(s =>
                s.Position != null && s.Position.ToLower().Contains(keyword));
        }

        var totalItems = await sessions.CountAsync(ct);

        var items = await sessions
            .OrderByDescending(s => s.EndedAt ?? s.UpdatedAt)
            .ThenByDescending(s => s.CreatedAt)
            .ThenBy(s => s.SessionId)
            .Skip((normalized.Page - 1) * normalized.PageSize)
            .Take(normalized.PageSize)
            .ToListAsync(ct);

        return (items, totalItems, positionOptions);
    }

    public Task<InterviewSession?> GetChatLogAsync(Guid userId, Guid sessionId, CancellationToken ct) =>
        _db.InterviewSessions
            .AsNoTracking()
            .Include(s => s.SessionQuestions.OrderBy(q => q.OrderIndex).ThenBy(q => q.SessionQuestionId))
                .ThenInclude(q => q.ChatMessages.OrderBy(m => m.SentAt).ThenBy(m => m.MessageId))
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.UserId == userId && !s.IsDeleted, ct);

    public Task<int> CountFeedbackEligibleSessionsAsync(Guid userId, CancellationToken ct) =>
        _db.InterviewSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .Where(s => s.Status == "Completed" || s.Status == "Expired")
            .CountAsync(ct);

    public Task<InterviewSession?> GetFeedbackEligibilitySessionAsync(Guid userId, Guid sessionId, CancellationToken ct) =>
        _db.InterviewSessions
            .AsNoTracking()
            .Where(s => s.SessionId == sessionId && s.UserId == userId && !s.IsDeleted)
            .Select(s => new InterviewSession
            {
                SessionId = s.SessionId,
                UserId = s.UserId,
                Status = s.Status,
                IsDeleted = s.IsDeleted
            })
            .FirstOrDefaultAsync(ct);
    public Task<InterviewRoom?> GetRoomBySessionAsync(Guid sessionId, CancellationToken ct) =>
        _db.InterviewRooms.FirstOrDefaultAsync(r => r.SessionId == sessionId, ct);

    // Room đã được tracking bởi GetRoomBySessionAsync; gọi SaveChanges để lưu các thay đổi đã set trên entity.
    public Task UpdateRoomAsync(InterviewRoom room, CancellationToken ct) =>
        _db.SaveChangesAsync(ct);

    public async Task<bool> UpdateStatusIfAsync(Guid userId, Guid sessionId, string currentStatus, string newStatus, CancellationToken ct)
    {
        var rowsAffected = await _db.InterviewSessions
            .Where(s => s.SessionId == sessionId && s.UserId == userId && s.Status == currentStatus && !s.IsDeleted)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, newStatus)
                                      .SetProperty(x => x.UpdatedAt, VietnamTime.Now), ct);
        _logger.LogInformation("UpdateStatusIfAsync: rowsAffected={RowsAffected}, params(sessionId={SessionId}, userId={UserId}, currentStatus={CurrentStatus}, newStatus={NewStatus})",
            rowsAffected, sessionId, userId, currentStatus, newStatus);
        return rowsAffected > 0;
    }

    public async Task<bool> IncrementAnsweredIfAsync(Guid sessionId, int expectedCurrentCount, CancellationToken ct)
    {
        var rowsAffected = await _db.InterviewSessions
            .Where(s => s.SessionId == sessionId && s.AnsweredQuestions == expectedCurrentCount && !s.IsDeleted)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AnsweredQuestions, x => x.AnsweredQuestions + 1)
                                      .SetProperty(x => x.UpdatedAt, VietnamTime.Now), ct);
        _logger.LogInformation(
            "IncrementAnsweredIfAsync: rowsAffected={RowsAffected}, sessionId={SessionId}, expectedCurrentCount={ExpectedCurrentCount}",
            rowsAffected, sessionId, expectedCurrentCount);
        return rowsAffected > 0;
    }

    public async Task DecrementAnsweredAsync(Guid sessionId, CancellationToken ct)
    {
        await _db.InterviewSessions
            .Where(s => s.SessionId == sessionId && !s.IsDeleted)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AnsweredQuestions, x => x.AnsweredQuestions - 1)
                                      .SetProperty(x => x.UpdatedAt, VietnamTime.Now), ct);
    }

    // Flag-only audit counter (mandatory-camera policy) — plain increment, no expected-value
    // claim needed like IncrementAnsweredIfAsync: this is just a tally, not a per-turn guard.
    public async Task<int> IncrementCameraViolationAsync(Guid sessionId, CancellationToken ct)
    {
        await _db.InterviewSessions
            .Where(s => s.SessionId == sessionId && !s.IsDeleted)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CameraViolationCount, x => x.CameraViolationCount + 1)
                                      .SetProperty(x => x.UpdatedAt, VietnamTime.Now), ct);

        // ExecuteUpdateAsync bypasses the change tracker — re-read the confirmed new value
        // instead of trusting an in-memory guess (same reasoning as elsewhere in this file).
        var count = await _db.InterviewSessions.AsNoTracking()
            .Where(s => s.SessionId == sessionId)
            .Select(s => s.CameraViolationCount)
            .FirstOrDefaultAsync(ct);
        return count;
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}

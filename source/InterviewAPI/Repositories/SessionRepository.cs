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

    // Lấy tất cả phiên của một user (không bao gồm đã xoá mềm), mới nhất trước.
    public Task<List<InterviewSession>> GetByUserAsync(Guid userId, CancellationToken ct) =>
        _db.InterviewSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

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

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}

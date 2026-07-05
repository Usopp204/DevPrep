using InterviewAPI.Models;

namespace InterviewAPI.Repositories.Interfaces;

public interface ISessionRepository
{
    Task<InterviewSession?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<InterviewSession?> GetWithQuestionsAsync(Guid sessionId, CancellationToken ct);
    Task<InterviewSession?> GetWithFullHistoryAsync(Guid sessionId, CancellationToken ct);
    Task<InterviewSession> CreateAsync(InterviewSession session, CancellationToken ct);
    Task AddSessionQuestionsAsync(List<SessionQuestion> questions, CancellationToken ct);
    Task<InterviewRoom> CreateRoomAsync(InterviewRoom room, CancellationToken ct);
    Task<ChatMessage> AddChatMessageAsync(ChatMessage message, CancellationToken ct);
    Task<SessionQuestion?> GetQuestionByIdAsync(Guid sessionQuestionId, CancellationToken ct);
    Task<List<ChatMessage>> GetMessagesByQuestionAsync(Guid sessionQuestionId, CancellationToken ct);
    Task<SessionQuestion?> GetNextUnansweredQuestionAsync(Guid sessionId, int afterOrderIndex, CancellationToken ct);
    Task<List<InterviewSession>> GetByUserAsync(Guid userId, CancellationToken ct);
    Task<InterviewRoom?> GetRoomBySessionAsync(Guid sessionId, CancellationToken ct);
    Task UpdateRoomAsync(InterviewRoom room, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

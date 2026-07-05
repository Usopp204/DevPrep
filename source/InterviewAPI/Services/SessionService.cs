using InterviewAPI.Clients;
using InterviewAPI.DTOs.AiService;
using InterviewAPI.DTOs.Evaluation;
using InterviewAPI.DTOs.Session;
using InterviewAPI.Helpers;
using InterviewAPI.Models;
using InterviewAPI.Repositories.Interfaces;
using InterviewAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewAPI.Services;

public class SessionService : ISessionService
{
    private static readonly int[] ValidDurations = { 15, 30, 45, 60 };
    private static readonly string[] ValidLevels = { "Intern", "Fresher", "Junior", "Middle", "Senior", "Lead" };
    private static readonly string[] ValidModes = { "Text", "Voice" };

    private readonly ISessionRepository _repo;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly InterviewDbContext _db;
    private readonly ILogger<SessionService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SessionService(
        ISessionRepository repo,
        IAiServiceClient aiServiceClient,
        InterviewDbContext db,
        ILogger<SessionService> logger,
        IServiceScopeFactory scopeFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        _repo = repo;
        _aiServiceClient = aiServiceClient;
        _db = db;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ConfigResponse> ConfigDurationAsync(Guid userId, ConfigDurationRequest req, CancellationToken ct)
    {
        if (!ValidDurations.Contains(req.DurationMinutes))
            throw new InvalidOperationException("Invalid duration. Must be 15, 30, 45, or 60.");

        var session = await ResolveSessionAsync(req.SessionId, userId, ct,
            factory: () => new InterviewSession
            {
                SessionId = Guid.NewGuid(),
                UserId = userId,
                Status = "Configured",
                Level = "Junior",
                InteractionMode = "Text",
                QuestionType = "Mixed",
                DurationMinutes = req.DurationMinutes,
                CreatedAt = VietnamTime.Now,
                UpdatedAt = VietnamTime.Now
            });

        if (req.SessionId.HasValue && req.SessionId != Guid.Empty)
        {
            session.DurationMinutes = req.DurationMinutes;
            session.UpdatedAt = VietnamTime.Now;
            await _repo.SaveChangesAsync(ct);
        }

        return new ConfigResponse
        {
            SessionId = session.SessionId,
            UpdatedField = "DurationMinutes",
            UpdatedValue = session.DurationMinutes.ToString()
        };
    }

    public async Task<ConfigResponse> ConfigLevelAsync(Guid userId, ConfigLevelRequest req, CancellationToken ct)
    {
        if (!ValidLevels.Contains(req.Level))
            throw new InvalidOperationException("Invalid level.");

        var session = await ResolveSessionAsync(req.SessionId, userId, ct,
            factory: () => new InterviewSession
            {
                SessionId = Guid.NewGuid(),
                UserId = userId,
                Status = "Configured",
                Level = req.Level,
                InteractionMode = "Text",
                QuestionType = "Mixed",
                DurationMinutes = 30,
                CreatedAt = VietnamTime.Now,
                UpdatedAt = VietnamTime.Now
            });

        if (req.SessionId.HasValue && req.SessionId != Guid.Empty)
        {
            session.Level = req.Level;
            session.UpdatedAt = VietnamTime.Now;
            await _repo.SaveChangesAsync(ct);
        }

        return new ConfigResponse
        {
            SessionId = session.SessionId,
            UpdatedField = "Level",
            UpdatedValue = session.Level
        };
    }

    public async Task<ConfigResponse> ConfigInteractionAsync(Guid userId, ConfigInteractionRequest req, CancellationToken ct)
    {
        if (!ValidModes.Contains(req.Mode))
            throw new InvalidOperationException("Invalid interaction mode.");

        var session = await ResolveSessionAsync(req.SessionId, userId, ct,
            factory: () => new InterviewSession
            {
                SessionId = Guid.NewGuid(),
                UserId = userId,
                Status = "Configured",
                Level = "Junior",
                InteractionMode = req.Mode,
                QuestionType = "Mixed",
                DurationMinutes = 30,
                CreatedAt = VietnamTime.Now,
                UpdatedAt = VietnamTime.Now
            });

        if (req.SessionId.HasValue && req.SessionId != Guid.Empty)
        {
            session.InteractionMode = req.Mode;
            session.UpdatedAt = VietnamTime.Now;
            await _repo.SaveChangesAsync(ct);
        }

        return new ConfigResponse
        {
            SessionId = session.SessionId,
            UpdatedField = "InteractionMode",
            UpdatedValue = session.InteractionMode
        };
    }

    public async Task<InitResponse> InitAsync(Guid userId, InitRequest req, CancellationToken ct)
    {
        // Step A: Load and verify the draft session.
        var session = await _repo.GetByIdAsync(req.SessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");
        if (session.Status != "Configured")
            throw new InvalidOperationException("Session already initialized.");

        // Step B: TODO — deduct credit via PaymentApiClient (PaymentAPI not implemented yet).
        // const bool CreditsEnabled = false;
        // if (CreditsEnabled) await _paymentClient.DeductCreditAsync(userId, ct);

        // Step B.1: Persist the chosen role on the session (used by the plan below and shown later
        // in History/Report). Falls back to the existing/default when the caller omits it.
        if (!string.IsNullOrWhiteSpace(req.Position))
            session.Position = req.Position;

        // Step C: Ask ai-service to plan all questions for this session, tailored by role/focus/JD.
        var planRequest = new AiQuestionPlanRequest
        {
            Position = session.Position ?? "Backend",
            Level = session.Level,
            FocusAreas = req.FocusAreas ?? new List<string>(),
            DurationMin = session.DurationMinutes,
            CvText = null,
            JdText = req.Jd
        };
        _logger.LogInformation(
            "InitAsync: calling ai-service PlanQuestions for session {SessionId} (position={Position}, level={Level}, durationMin={DurationMin}).",
            session.SessionId, planRequest.Position, planRequest.Level, planRequest.DurationMin);

        var plan = await _aiServiceClient.PlanQuestionsAsync(planRequest, ct);

        // Log số câu hỏi ai-service trả về — nếu 0 thì response rỗng hoặc deserialize sai.
        var returnedCount = plan?.Questions?.Count ?? 0;
        _logger.LogInformation(
            "InitAsync: ai-service returned {Count} planned questions for session {SessionId}.",
            returnedCount, session.SessionId);

        if (returnedCount == 0)
        {
            _logger.LogError(
                "InitAsync: ai-service returned NO questions for session {SessionId}. " +
                "Aborting init so the session is not left empty/half-initialized.",
                session.SessionId);
            throw new InvalidOperationException("AI service returned no questions. Please try again.");
        }

        // Step D: Map ai-service response to SessionQuestion entities.
        // ReferenceAnswer is stored server-side only — never returned to the browser.
        var questions = plan!.Questions.Select(q => new SessionQuestion
        {
            SessionQuestionId = Guid.NewGuid(),
            SessionId = session.SessionId,
            OrderIndex = q.OrderIndex,
            Content = q.Question,
            ReferenceAnswer = q.ReferenceAnswer,
            IsAiGenerated = true,
            ContextSource = "QuestionBank",
            AskedAt = null
        }).ToList();

        // Step E: Persist all questions in one batch, then verify the insert succeeded.
        try
        {
            await _repo.AddSessionQuestionsAsync(questions, ct);
            _logger.LogInformation(
                "InitAsync: inserted {Count} SessionQuestions for session {SessionId}.",
                questions.Count, session.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "InitAsync: failed to insert {Count} SessionQuestions for session {SessionId}.",
                questions.Count, session.SessionId);
            throw;
        }

        // Step F: Transition session to Initialized state.
        session.Status = "Initialized";
        session.StartedAt = VietnamTime.Now;
        session.UpdatedAt = VietnamTime.Now;
        session.TotalQuestions = questions.Count;
        await _repo.SaveChangesAsync(ct);

        // Step G: Open the interview room.
        var room = await _repo.CreateRoomAsync(new InterviewRoom
        {
            RoomId = Guid.NewGuid(),
            SessionId = session.SessionId,
            ChannelName = "room-" + session.SessionId,
            Status = "Active",
            InitializedAt = VietnamTime.Now
        }, ct);

        // Step H: Return enough for the client to open the room and show the first question.
        // Lấy câu có OrderIndex nhỏ nhất (bền với cả đánh số bắt đầu từ 0 hay 1).
        var firstQuestion = questions.OrderBy(q => q.OrderIndex).FirstOrDefault()?.Content ?? string.Empty;
        return new InitResponse
        {
            SessionId = session.SessionId,
            TotalQuestions = questions.Count,
            RoomId = room.RoomId,
            FirstQuestion = firstQuestion
        };
    }

    public async Task<GenerateQuestionResponse> GenerateQuestionAsync(
        Guid userId, Guid sessionId, CancellationToken ct)
    {
        // Step A: Load session kèm câu hỏi, kiểm tra quyền và trạng thái.
        var session = await _repo.GetWithQuestionsAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");
        if (session.Status != "Initialized" && session.Status != "InProgress")
            throw new InvalidOperationException("Session not available.");

        // Step B: Tìm câu hỏi đầu tiên chưa có câu trả lời của User.
        SessionQuestion? nextQuestion = null;
        foreach (var q in session.SessionQuestions) // đã OrderBy OrderIndex từ GetWithQuestionsAsync
        {
            var hasAnswer = await _db.ChatMessages.AnyAsync(
                m => m.SessionQuestionId == q.SessionQuestionId
                  && m.SenderType == "User"
                  && m.MessageType == "Answer",
                ct);
            if (!hasAnswer)
            {
                nextQuestion = q;
                break;
            }
        }

        // Đã trả lời hết tất cả câu hỏi → kết thúc phiên.
        if (nextQuestion is null)
            return new GenerateQuestionResponse { Ended = true, TotalQuestions = session.TotalQuestions };

        // Step C + D: Cập nhật AskedAt và chuyển trạng thái session — gộp 1 lần save.
        bool dirty = false;
        if (nextQuestion.AskedAt is null)
        {
            nextQuestion.AskedAt = VietnamTime.Now;
            dirty = true;
        }
        if (session.Status == "Initialized")
        {
            session.Status = "InProgress";
            session.UpdatedAt = VietnamTime.Now;
            dirty = true;
        }
        if (dirty) await _repo.SaveChangesAsync(ct);

        // Step E: Lưu tin nhắn câu hỏi của AI vào ChatMessages.
        await _repo.AddChatMessageAsync(new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            SessionQuestionId = nextQuestion.SessionQuestionId,
            SenderType = "AI",
            MessageType = "Question",
            Content = nextQuestion.Content,
            SentAt = VietnamTime.Now
        }, ct);

        // Step F: Trả về thông tin câu hỏi tiếp theo (không kèm ReferenceAnswer).
        return new GenerateQuestionResponse
        {
            SessionQuestionId = nextQuestion.SessionQuestionId,
            OrderIndex = nextQuestion.OrderIndex,
            Content = nextQuestion.Content,
            TotalQuestions = session.TotalQuestions,
            Ended = false
        };
    }

    public async Task<MessageResponse> SendMessageAsync(
        Guid userId, Guid sessionId, SendMessageRequest req, CancellationToken ct)
    {
        // Step A: Kiểm tra phiên thuộc về user và đang InProgress.
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");
        if (session.Status != "InProgress")
            throw new InvalidOperationException("Session is not in progress.");

        // Step B: Kiểm tra câu hỏi thuộc phiên này.
        var currentQuestion = await _repo.GetQuestionByIdAsync(req.SessionQuestionId, ct);
        if (currentQuestion is null || currentQuestion.SessionId != sessionId)
            throw new InvalidOperationException("Question not found.");

        // Step C: Lưu câu trả lời của user vào ChatMessages.
        await _repo.AddChatMessageAsync(new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            SessionQuestionId = req.SessionQuestionId,
            SenderType = "User",
            MessageType = "Answer",
            Content = req.Content,
            ResponseTimeMs = req.ResponseTimeMs,
            SentAt = VietnamTime.Now
        }, ct);

        // Step D: Tăng số câu đã trả lời, lưu session.
        session.AnsweredQuestions += 1;
        session.UpdatedAt = VietnamTime.Now;
        await _repo.SaveChangesAsync(ct);

        // Step D.1: Grade this answer in the background so the interview turn stays fast and the
        // report is ready by the time the user reaches it. Empty content (Skip) is graded as
        // score 0 "No answer provided." by ai-service — no special handling needed here.
        ScheduleGrading(sessionId, currentQuestion, req.Content);

        // Step E: Tìm câu hỏi tiếp theo chưa được trả lời.
        var nextQuestion = await _repo.GetNextUnansweredQuestionAsync(
            sessionId, currentQuestion.OrderIndex, ct);

        // Step F: Còn câu hỏi tiếp theo — phát câu hỏi AI, cập nhật AskedAt.
        if (nextQuestion is not null)
        {
            await _repo.AddChatMessageAsync(new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                SessionQuestionId = nextQuestion.SessionQuestionId,
                SenderType = "AI",
                MessageType = "Question",
                Content = nextQuestion.Content,
                SentAt = VietnamTime.Now
            }, ct);

            nextQuestion.AskedAt = VietnamTime.Now;
            await _repo.SaveChangesAsync(ct);

            return new MessageResponse
            {
                Ended = false,
                NextQuestion = new NextQuestionDto
                {
                    SessionQuestionId = nextQuestion.SessionQuestionId,
                    OrderIndex = nextQuestion.OrderIndex,
                    Content = nextQuestion.Content
                }
            };
        }

        // Step G: Đã trả lời hết — đóng phiên và phòng phỏng vấn.
        session.Status = "Completed";
        session.EndedAt = VietnamTime.Now;
        session.UpdatedAt = VietnamTime.Now;

        var room = await _repo.GetRoomBySessionAsync(sessionId, ct);
        if (room is not null)
        {
            room.Status = "Closed";
            room.ClosedAt = VietnamTime.Now;
            await _repo.UpdateRoomAsync(room, ct); // lưu cả session + room cùng lúc
        }
        else
        {
            await _repo.SaveChangesAsync(ct); // chỉ lưu session
        }

        return new MessageResponse { Ended = true, NextQuestion = null };
    }

    public async Task<TimerResponse> GetTimerAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");

        int remaining;
        if (session.StartedAt is null)
        {
            remaining = session.DurationMinutes * 60;
        }
        else
        {
            var elapsed = (int)(VietnamTime.Now - session.StartedAt.Value).TotalSeconds;
            remaining = Math.Max(0, session.DurationMinutes * 60 - elapsed);
        }

        return new TimerResponse { RemainingSeconds = remaining, Status = session.Status };
    }

    public async Task TrackTimeAsync(Guid userId, Guid sessionId, TrackTimeRequest req, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");

        // Cập nhật ResponseTimeMs cho tin nhắn User Answer của câu hỏi cụ thể.
        var message = await _db.ChatMessages.FirstOrDefaultAsync(
            m => m.SessionQuestionId == req.SessionQuestionId
              && m.SenderType == "User"
              && m.MessageType == "Answer",
            ct);

        if (message is not null)
        {
            message.ResponseTimeMs = req.ResponseTimeMs;
            await _repo.SaveChangesAsync(ct);
        }

        // Tính lại trung bình thời gian phản hồi trên toàn phiên.
        var avgMs = await _db.ChatMessages
            .Where(m => m.SessionQuestion.SessionId == sessionId
                     && m.SenderType == "User"
                     && m.MessageType == "Answer"
                     && m.ResponseTimeMs != null)
            .AverageAsync(m => (double?)m.ResponseTimeMs, ct);

        if (avgMs.HasValue)
        {
            session.AvgResponseTimeMs = (int)avgMs.Value;
            session.UpdatedAt = VietnamTime.Now;
            await _repo.SaveChangesAsync(ct);
        }
    }

    public async Task<EndResponse> EndSessionAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");
        if (session.Status != "Initialized" && session.Status != "InProgress")
            throw new InvalidOperationException("Session already ended.");

        // Hết giờ → Expired; kết thúc sớm → Completed.
        if (session.StartedAt is not null &&
            (VietnamTime.Now - session.StartedAt.Value).TotalMinutes >= session.DurationMinutes)
            session.Status = "Expired";
        else
            session.Status = "Completed";

        session.EndedAt = VietnamTime.Now;
        session.UpdatedAt = VietnamTime.Now;

        var room = await _repo.GetRoomBySessionAsync(sessionId, ct);
        if (room is not null)
        {
            room.Status = "Closed";
            room.ClosedAt = VietnamTime.Now;
            await _repo.UpdateRoomAsync(room, ct);
        }
        else
        {
            await _repo.SaveChangesAsync(ct);
        }

        return new EndResponse { Status = session.Status, EndedAt = session.EndedAt };
    }

    public async Task<ResumeResponse> ResumeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetWithFullHistoryAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");

        // Tính thời gian còn lại.
        int remaining;
        if (session.StartedAt is null)
        {
            remaining = session.DurationMinutes * 60;
        }
        else
        {
            var elapsed = (int)(VietnamTime.Now - session.StartedAt.Value).TotalSeconds;
            remaining = Math.Max(0, session.DurationMinutes * 60 - elapsed);
        }

        // Tìm câu hỏi hiện tại chưa có User Answer (dùng in-memory vì ChatMessages đã load sẵn).
        NextQuestionDto? currentQuestion = null;
        foreach (var q in session.SessionQuestions) // đã OrderBy OrderIndex
        {
            var hasAnswer = q.ChatMessages.Any(m => m.SenderType == "User" && m.MessageType == "Answer");
            if (!hasAnswer)
            {
                currentQuestion = new NextQuestionDto
                {
                    SessionQuestionId = q.SessionQuestionId,
                    OrderIndex = q.OrderIndex,
                    Content = q.Content
                };
                break;
            }
        }

        // Gộp toàn bộ lịch sử chat từ tất cả câu hỏi, sắp xếp theo thời gian.
        var chatHistory = session.SessionQuestions
            .SelectMany(q => q.ChatMessages.Select(m => new ChatHistoryItem
            {
                MessageId = m.MessageId,
                SessionQuestionId = m.SessionQuestionId,
                SenderType = m.SenderType,
                MessageType = m.MessageType,
                Content = m.Content,
                SentAt = m.SentAt,
                ResponseTimeMs = m.ResponseTimeMs
            }))
            .OrderBy(m => m.SentAt)
            .ToList();

        return new ResumeResponse
        {
            Status = session.Status,
            Position = session.Position,
            Level = session.Level,
            RemainingSeconds = remaining,
            CurrentQuestion = currentQuestion,
            TotalQuestions = session.TotalQuestions,
            AnsweredQuestions = session.AnsweredQuestions,
            ChatHistory = chatHistory
        };
    }

    public async Task SoftDeleteAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");

        session.IsDeleted = true;
        session.DeletedAt = VietnamTime.Now;
        session.UpdatedAt = VietnamTime.Now;
        await _repo.SaveChangesAsync(ct);
    }

    public async Task<List<SessionListItemDto>> GetMySessionsAsync(Guid userId, CancellationToken ct)
    {
        var sessions = await _repo.GetByUserAsync(userId, ct);
        return sessions.Select(s => new SessionListItemDto
        {
            SessionId = s.SessionId,
            Title = s.Title,
            Position = s.Position,
            Level = s.Level,
            Status = s.Status,
            DurationMinutes = s.DurationMinutes,
            TotalQuestions = s.TotalQuestions,
            AnsweredQuestions = s.AnsweredQuestions,
            StartedAt = s.StartedAt,
            EndedAt = s.EndedAt,
            CreatedAt = s.CreatedAt
        }).ToList();
    }

    public async Task<TranscriptResponse> GetTranscriptAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        // Dùng lại GetWithFullHistoryAsync (đã load câu hỏi + chat) — phương thức đã tồn tại, dùng cho resume.
        var session = await _repo.GetWithFullHistoryAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");

        // Không giới hạn Status — transcript hoạt động với mọi trạng thái (Completed, Expired, InProgress...).
        // Chỉ giữ câu ĐÃ THỰC SỰ ĐƯỢC HỎI: mọi câu được tạo hàng loạt lúc init, nhưng câu chỉ có
        // ChatMessage khi đã phát ra (trả lời / skip / hết giờ chưa trả lời). Câu chưa từng tới
        // không có ChatMessage → ẩn khỏi transcript (dùng chung cho cả History và Report).
        var questions = session.SessionQuestions
            .Where(q => q.ChatMessages.Any())
            .Select(q => new TranscriptQuestionDto
            {
                SessionQuestionId = q.SessionQuestionId,
                OrderIndex = q.OrderIndex,
                Content = q.Content,
                Category = null,   // DB không lưu Category riêng cho SessionQuestion
                Difficulty = null  // DB không lưu Difficulty riêng cho SessionQuestion
            }).ToList();

        var chatHistory = session.SessionQuestions
            .SelectMany(q => q.ChatMessages.Select(m => new ChatHistoryItem
            {
                MessageId = m.MessageId,
                SessionQuestionId = m.SessionQuestionId,
                SenderType = m.SenderType,
                MessageType = m.MessageType,
                Content = m.Content,
                SentAt = m.SentAt,
                ResponseTimeMs = m.ResponseTimeMs
            }))
            .OrderBy(m => m.SentAt)
            .ToList();

        return new TranscriptResponse
        {
            SessionId = session.SessionId,
            Title = session.Title,
            Position = session.Position,
            Level = session.Level,
            Status = session.Status,
            DurationMinutes = session.DurationMinutes,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            TotalQuestions = session.TotalQuestions,
            AnsweredQuestions = session.AnsweredQuestions,
            Questions = questions,
            ChatHistory = chatHistory
        };
    }

    // Nếu sessionId rỗng: tạo mới (gọi factory để build entity với trường đã set sẵn, lưu một lần).
    // Nếu có sessionId: load, kiểm tra quyền sở hữu và trạng thái.
    private async Task<InterviewSession> ResolveSessionAsync(
        Guid? sessionId, Guid userId, CancellationToken ct, Func<InterviewSession> factory)
    {
        if (!sessionId.HasValue || sessionId == Guid.Empty)
            return await _repo.CreateAsync(factory(), ct);

        var session = await _repo.GetByIdAsync(sessionId.Value, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");
        if (session.Status != "Configured")
            throw new InvalidOperationException("Session already started.");
        return session;
    }

    public async Task<bool> OwnsSessionAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        return session is not null && session.UserId == userId;
    }

    // Fire-and-forget grading of one answer via EvaluationAPI. Runs in its own DI scope (the request
    // scope is gone once the response returns), forwards the caller's bearer token, and swallows
    // errors (grading is best-effort; a missed answer is simply omitted from the report).
    // Category/Difficulty are left empty here — categorization is handled by the report step (Part D).
    private void ScheduleGrading(Guid sessionId, SessionQuestion question, string userAnswer)
    {
        // Capture the token synchronously — HttpContext is not available inside the background task.
        var bearerToken = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        var payload = new EvaluateAnswerPayload
        {
            Question = question.Content,
            ReferenceAnswer = question.ReferenceAnswer ?? string.Empty,
            Category = string.Empty,
            Difficulty = string.Empty,
            UserAnswer = userAnswer
        };
        var questionId = question.SessionQuestionId;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<IEvaluationApiClient>();
                await client.EvaluateAnswerAsync(sessionId, questionId, payload, bearerToken, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Background grading failed for question {QuestionId} in session {SessionId}.",
                    questionId, sessionId);
            }
        });
    }
}

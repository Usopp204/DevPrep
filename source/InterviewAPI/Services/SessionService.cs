using InterviewAPI.DTOs;
using InterviewAPI.Clients;
using InterviewAPI.DTOs.AiService;
using InterviewAPI.DTOs.Evaluation;
using InterviewAPI.DTOs.Session;
using InterviewAPI.Helpers;
using InterviewAPI.Models;
using InterviewAPI.Repositories.Interfaces;
using InterviewAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InterviewAPI.Services;

public class SessionService : ISessionService
{

    private static readonly int[] ValidDurations = { 15, 30, 45, 60 };
    private static readonly string[] ValidLevels = { "Intern", "Fresher", "Junior", "Middle", "Senior", "Lead" };
    private static readonly string[] ValidModes = { "Text", "Voice" };

    private static bool IsTerminalStatus(string status) =>
        status is "Completed" or "Cancelled" or "Expired";

    // Idle-reminder / auto-advance thresholds (seconds since AskedAt), by question difficulty.
    // NOTE: SessionQuestion has no persisted Difficulty column (ai-service's plan-time
    // Category/Difficulty is discarded when Init maps to SessionQuestion — see InitAsync Step D),
    // so GetIdleThresholds is always called with null today and always falls back to Medium.
    private static readonly Dictionary<string, (int ReminderSeconds, int HardSeconds)> IdleThresholds = new()
    {
        ["Easy"] = (60, 120),
        ["Medium"] = (90, 180),
        ["Hard"] = (150, 300)
    };

    private static (int ReminderSeconds, int HardSeconds) GetIdleThresholds(string? difficulty)
    {
        if (difficulty is not null && IdleThresholds.TryGetValue(difficulty, out var t))
            return t;
        return IdleThresholds["Medium"];
    }

    // Pause/Resume limits (Room UX Phase 2) — flat per-session caps, not scaled by DurationMinutes.
    private const int MaxPauseCount = 3;
    private const int MaxTotalPausedSeconds = 300;

    // Every timer formula (remaining time, Expired detection) must subtract this — it's the ONLY
    // correct way to keep the countdown frozen while paused: while an ongoing pause is active
    // (PausedAt is set), Now and the in-progress pause delta grow together and cancel out.
    private static int GetEffectivePausedSeconds(InterviewSession session)
    {
        var ongoing = session.PausedAt is null
            ? 0
            : (int)(VietnamTime.Now - session.PausedAt.Value).TotalSeconds;
        return session.TotalPausedSeconds + ongoing;
    }

    private readonly ISessionRepository _repo;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IPaymentApiClient _paymentApiClient;
    private readonly InterviewDbContext _db;
    private readonly ILogger<SessionService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SessionService(
        ISessionRepository repo,
        IAiServiceClient aiServiceClient,
        IPaymentApiClient paymentApiClient,
        InterviewDbContext db,
        ILogger<SessionService> logger,
        IServiceScopeFactory scopeFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        _repo = repo;
        _aiServiceClient = aiServiceClient;
        _paymentApiClient = paymentApiClient;
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

        // Step C: Set AskedAt on first question serve.
        if (nextQuestion.AskedAt is null)
        {
            nextQuestion.AskedAt = VietnamTime.Now;
            await _repo.SaveChangesAsync(ct);
        }

        // Step D: Atomic status update (Initialized → InProgress) + deduct credit on first entry.
        bool isFirstEntry = session.Status == "Initialized";
        _logger.LogInformation("GenerateQuestion: session {SessionId}, isFirstEntry={isFirstEntry}, sessionStatus={Status}, userId={UserId}",
            sessionId, isFirstEntry, session.Status, userId);
        bool statusChanged = await _repo.UpdateStatusIfAsync(userId, sessionId, "Initialized", "InProgress", ct);
        _logger.LogInformation("GenerateQuestion: UpdateStatusIfAsync returned {StatusChanged}", statusChanged);

        // Step D.1: Deduct credit if this is the first entry (status changed successfully).
        int creditCost = 0;
        if (isFirstEntry && statusChanged)
        {
            creditCost = CreditConfig.GetCreditCost(session.DurationMinutes);
            var deductResult = await _paymentApiClient.DeductCreditAsync(userId, creditCost, sessionId,
                $"Interview session {session.DurationMinutes} minutes", ct);

            if (!deductResult.Success)
            {
                _logger.LogError("Credit deduction failed: {Message}", deductResult.Message);
                // Rollback status back to Initialized (race-safe: only we can update from InProgress in this flow).
                await _repo.UpdateStatusIfAsync(userId, sessionId, "InProgress", "Initialized", ct);
                throw new InvalidOperationException($"Credit deduction failed: {deductResult.Message}");
            }

            // Update session with credit cost.
            var refreshedSession = await _repo.GetByIdAsync(sessionId, ct);
            if (refreshedSession is not null)
            {
                session = refreshedSession;
                session.CreditCost = creditCost;
                await _repo.SaveChangesAsync(ct);
            }
        }

        // Step E: Lưu tin nhắn câu hỏi của AI vào ChatMessages.
        try
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
        }
        catch (Exception ex)
        {
            // If chat message fails after credit deduction, refund the credit.
            if (isFirstEntry && statusChanged && creditCost > 0)
            {
                _logger.LogError(ex, "Failed to save chat message after credit deduction. Refunding credit.");
                await _paymentApiClient.RefundCreditAsync(userId, creditCost, sessionId,
                    "Refund: failed to initialize session", ct);
            }
            throw;
        }

        // Step F: Trả về thông tin câu hỏi tiếp theo (không kèm ReferenceAnswer).
        var (reminderSeconds, hardSeconds) = GetIdleThresholds(null);
        var elapsedSeconds = Math.Max(0, (int)(VietnamTime.Now - nextQuestion.AskedAt!.Value).TotalSeconds - GetEffectivePausedSeconds(session));
        return new GenerateQuestionResponse
        {
            SessionQuestionId = nextQuestion.SessionQuestionId,
            OrderIndex = nextQuestion.OrderIndex,
            Content = nextQuestion.Content,
            TotalQuestions = session.TotalQuestions,
            Ended = false,
            IdleReminderSeconds = reminderSeconds,
            IdleHardSeconds = hardSeconds,
            IdleElapsedSeconds = elapsedSeconds
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

        // Step C: Xác thực reason client gửi lên — "TimedOut" chỉ được tin khi server tự tính lại
        // idle từ AskedAt (nguồn sự thật) đã vượt ngưỡng cứng; nếu không đủ, không tin nhãn đó.
        // "Skipped" là hành động chủ động tức thời của user nên không cần kiểm tra idle.
        string? answerReason = null;
        if (req.Reason == "Skipped")
        {
            answerReason = "Skipped";
        }
        else if (req.Reason == "TimedOut")
        {
            const int graceSeconds = 5;
            var (_, hardSeconds) = GetIdleThresholds(null);
            var elapsedSeconds = currentQuestion.AskedAt is null
                ? 0
                : (VietnamTime.Now - currentQuestion.AskedAt.Value).TotalSeconds - GetEffectivePausedSeconds(session);
            if (elapsedSeconds >= hardSeconds - graceSeconds)
                answerReason = "TimedOut";
        }

        // Step D: Lưu câu trả lời của user vào ChatMessages.
        await _repo.AddChatMessageAsync(new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            SessionQuestionId = req.SessionQuestionId,
            SenderType = "User",
            MessageType = "Answer",
            Content = req.Content,
            ResponseTimeMs = req.ResponseTimeMs,
            AnswerReason = answerReason,
            SentAt = VietnamTime.Now
        }, ct);

        // Step E: Tăng số câu đã trả lời, lưu session.
        session.AnsweredQuestions += 1;
        session.UpdatedAt = VietnamTime.Now;
        await _repo.SaveChangesAsync(ct);

        ScheduleGrading(sessionId, currentQuestion, req.Content);

        // Step F: Tìm câu hỏi tiếp theo chưa được trả lời.
        var nextQuestion = await _repo.GetNextUnansweredQuestionAsync(
            sessionId, currentQuestion.OrderIndex, ct);

        // Step G: Còn câu hỏi tiếp theo — phát câu hỏi AI, cập nhật AskedAt.
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

            var (reminderSeconds, hardSeconds) = GetIdleThresholds(null);
            return new MessageResponse
            {
                Ended = false,
                NextQuestion = new NextQuestionDto
                {
                    SessionQuestionId = nextQuestion.SessionQuestionId,
                    OrderIndex = nextQuestion.OrderIndex,
                    Content = nextQuestion.Content,
                    IdleReminderSeconds = reminderSeconds,
                    IdleHardSeconds = hardSeconds,
                    IdleElapsedSeconds = 0
                }
            };
        }

        // Step H: Đã trả lời hết — đóng phiên và phòng phỏng vấn.
        var hasSubstantiveAnswer = await _repo.HasSubstantiveAnswerAsync(sessionId, ct);
        session.Status = hasSubstantiveAnswer ? "Completed" : "Expired";
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
            var elapsed = (int)(VietnamTime.Now - session.StartedAt.Value).TotalSeconds - GetEffectivePausedSeconds(session);
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

    public async Task<EndResponse> CompleteSessionAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");
        if (IsTerminalStatus(session.Status))
            return new EndResponse { Status = session.Status, EndedAt = session.EndedAt };
        if (session.Status != "InProgress")
            throw new InvalidOperationException("Session cannot complete from its current state.");

        var elapsedSeconds = session.StartedAt is null
            ? 0
            : (VietnamTime.Now - session.StartedAt.Value).TotalSeconds - GetEffectivePausedSeconds(session);
        if (session.StartedAt is null || elapsedSeconds < session.DurationMinutes * 60)
            throw new InvalidOperationException("Session cannot complete before its deadline.");

        var room = await _repo.GetRoomBySessionAsync(sessionId, ct);
        var hasSubstantiveAnswer = await _repo.HasSubstantiveAnswerAsync(sessionId, ct);
        var candidateWasActiveAtDeadline = hasSubstantiveAnswer &&
                                           string.Equals(room?.Status, "Active", StringComparison.OrdinalIgnoreCase);
        session.Status = candidateWasActiveAtDeadline
            ? "Completed"
            : "Expired";
        session.EndedAt = VietnamTime.Now;
        session.UpdatedAt = session.EndedAt.Value;

        if (room is not null)
        {
            room.Status = "Closed";
            room.ClosedAt = session.EndedAt;
            await _repo.UpdateRoomAsync(room, ct);
        }
        else
        {
            await _repo.SaveChangesAsync(ct);
        }

        return new EndResponse { Status = session.Status, EndedAt = session.EndedAt };
    }

    public async Task<EndResponse> EndSessionAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");
        if (IsTerminalStatus(session.Status))
            return new EndResponse { Status = session.Status, EndedAt = session.EndedAt };
        if (session.Status is not ("Initialized" or "InProgress" or "Paused"))
            throw new InvalidOperationException("Session cannot be cancelled from its current state.");

        session.Status = "Cancelled";

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

    public async Task<PauseResponse> PauseSessionAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");
        if (session.Status != "InProgress")
            throw new InvalidOperationException("Only an in-progress session can be paused.");

        if (session.PauseCount >= MaxPauseCount)
            throw new InvalidOperationException($"Pause limit reached ({MaxPauseCount} pauses per session).");
        if (session.TotalPausedSeconds >= MaxTotalPausedSeconds)
            throw new InvalidOperationException($"Total pause time limit reached ({MaxTotalPausedSeconds} seconds per session).");

        session.Status = "Paused";
        session.PausedAt = VietnamTime.Now;
        session.PauseCount += 1;
        session.UpdatedAt = VietnamTime.Now;
        await _repo.SaveChangesAsync(ct);

        return new PauseResponse
        {
            Status = session.Status,
            PauseCount = session.PauseCount,
            PausesRemaining = Math.Max(0, MaxPauseCount - session.PauseCount),
            PausedSecondsRemaining = Math.Max(0, MaxTotalPausedSeconds - session.TotalPausedSeconds)
        };
    }

    public async Task<UnpauseResponse> UnpauseSessionAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");
        if (session.Status != "Paused" || session.PausedAt is null)
            throw new InvalidOperationException("Session is not paused.");

        session.TotalPausedSeconds += (int)(VietnamTime.Now - session.PausedAt.Value).TotalSeconds;
        session.PausedAt = null;
        session.Status = "InProgress";
        session.UpdatedAt = VietnamTime.Now;
        await _repo.SaveChangesAsync(ct);

        var elapsed = session.StartedAt is null
            ? 0
            : (int)(VietnamTime.Now - session.StartedAt.Value).TotalSeconds - GetEffectivePausedSeconds(session);
        var remaining = Math.Max(0, session.DurationMinutes * 60 - elapsed);

        return new UnpauseResponse
        {
            Status = session.Status,
            RemainingSeconds = remaining,
            PauseCount = session.PauseCount,
            PausesRemaining = Math.Max(0, MaxPauseCount - session.PauseCount),
            PausedSecondsRemaining = Math.Max(0, MaxTotalPausedSeconds - session.TotalPausedSeconds)
        };
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
            var elapsed = (int)(VietnamTime.Now - session.StartedAt.Value).TotalSeconds - GetEffectivePausedSeconds(session);
            remaining = Math.Max(0, session.DurationMinutes * 60 - elapsed);
        }

        // Tìm câu hỏi hiện tại chưa có User Answer (dùng in-memory vì ChatMessages đã load sẵn).
        NextQuestionDto? currentQuestion = null;
        foreach (var q in session.SessionQuestions) // đã OrderBy OrderIndex
        {
            var hasAnswer = q.ChatMessages.Any(m => m.SenderType == "User" && m.MessageType == "Answer");
            if (!hasAnswer)
            {
                var (reminderSeconds, hardSeconds) = GetIdleThresholds(null);
                currentQuestion = new NextQuestionDto
                {
                    SessionQuestionId = q.SessionQuestionId,
                    OrderIndex = q.OrderIndex,
                    Content = q.Content,
                    IdleReminderSeconds = reminderSeconds,
                    IdleHardSeconds = hardSeconds,
                    IdleElapsedSeconds = 0
                };
                break;
            }
        }

        // Room UX Phase 3 — true once this user has ever dismissed/completed the onboarding tour
        // (no row yet = never seen it). This is a nice-to-have onboarding flag, never allowed to
        // block entering the room — if the lookup fails for any reason (e.g. table not migrated
        // yet on this environment), fall back to "not seen" so the tour just shows once more
        // instead of taking down Resume, which is on the critical path to the interview itself.
        UserRoomTourStatus? tourStatus = null;
        try
        {
            tourStatus = await _db.UserRoomTourStatuses.AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ResumeSessionAsync: could not read UserRoomTourStatuses for user {UserId} — defaulting HasSeenRoomTour to false.",
                userId);
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
            QuestionType = session.QuestionType,
            RemainingSeconds = remaining,
            CurrentQuestion = currentQuestion,
            TotalQuestions = session.TotalQuestions,
            AnsweredQuestions = session.AnsweredQuestions,
            PauseCount = session.PauseCount,
            PausesRemaining = Math.Max(0, MaxPauseCount - session.PauseCount),
            PausedSecondsRemaining = Math.Max(0, MaxTotalPausedSeconds - session.TotalPausedSeconds),
            HasSeenRoomTour = tourStatus?.HasSeenRoomTour ?? false,
            ChatHistory = chatHistory
        };
    }

    public async Task MarkRoomTourSeenAsync(Guid userId, CancellationToken ct)
    {
        var status = await _db.UserRoomTourStatuses.FirstOrDefaultAsync(t => t.UserId == userId, ct);
        if (status is null)
        {
            _db.UserRoomTourStatuses.Add(new UserRoomTourStatus
            {
                UserId = userId,
                HasSeenRoomTour = true,
                UpdatedAt = VietnamTime.Now
            });
        }
        else if (!status.HasSeenRoomTour)
        {
            status.HasSeenRoomTour = true;
            status.UpdatedAt = VietnamTime.Now;
        }
        await _db.SaveChangesAsync(ct);
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



    public async Task<SessionHistoryResult> GetSessionHistoryAsync(
        Guid userId,
        SessionHistoryQuery query,
        CancellationToken ct)
    {
        var normalized = SessionHistoryQueryNormalizer.Normalize(query);
        var (sessions, totalItems, positionOptions) = await _repo.GetHistoryAsync(userId, normalized, ct);

        return new SessionHistoryResult
        {
            Items = sessions.Select(s => new SessionHistoryItemDto
            {
                SessionId = s.SessionId,
                Position = s.Position,
                Level = s.Level,
                InteractionMode = s.InteractionMode,
                QuestionType = s.QuestionType,
                Status = s.Status,
                DurationMinutes = s.DurationMinutes,
                TotalQuestions = s.TotalQuestions,
                AnsweredQuestions = s.AnsweredQuestions,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                CreatedAt = s.CreatedAt
            }).ToList(),
            Page = normalized.Page,
            PageSize = normalized.PageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)normalized.PageSize),
            PositionOptions = positionOptions
        };
    }

    public async Task<SessionChatLogResponse> GetSessionChatLogAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetChatLogAsync(userId, sessionId, ct);
        if (session is null)
            throw new KeyNotFoundException("Session not found.");

        if (!IsTerminalStatus(session.Status))
            throw new InvalidOperationException("Session is not eligible for chat log.");

        var questions = session.SessionQuestions
            .Where(q => q.ChatMessages.Any())
            .OrderBy(q => q.OrderIndex)
            .ThenBy(q => q.SessionQuestionId)
            .Select(q => new SessionChatLogQuestionDto
            {
                QuestionIndex = q.OrderIndex,
                QuestionText = q.Content,
                Messages = q.ChatMessages
                    .OrderBy(m => m.SentAt)
                    .ThenBy(m => m.MessageId)
                    .Select(m => new SessionChatLogMessageDto
                    {
                        MessageId = m.MessageId,
                        SenderRole = m.SenderType,
                        MessageType = m.MessageType,
                        Content = m.Content,
                        SentAt = m.SentAt,
                        ResponseDurationMs = m.ResponseTimeMs
                    })
                    .ToList()
            })
            .ToList();

        return new SessionChatLogResponse
        {
            SessionId = session.SessionId,
            SessionStatus = session.Status,
            Questions = questions
        };
    }

    public async Task<FeedbackEligibilityResponse> GetFeedbackEligibilityAsync(Guid userId, CancellationToken ct)
    {
        var count = await _repo.CountFeedbackEligibleSessionsAsync(userId, ct);
        return new FeedbackEligibilityResponse
        {
            Eligible = count > 0,
            QualifyingSessionCount = count
        };
    }

    public async Task<SessionFeedbackEligibilityResponse> GetSessionFeedbackEligibilityAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken ct)
    {
        var session = await _repo.GetFeedbackEligibilitySessionAsync(userId, sessionId, ct);
        if (session is null)
            throw new KeyNotFoundException("Session not found.");

        return new SessionFeedbackEligibilityResponse
        {
            SessionId = session.SessionId,
            Status = session.Status,
            Eligible = session.Status == "Completed"
        };
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

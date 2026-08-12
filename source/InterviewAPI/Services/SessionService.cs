using InterviewAPI.DTOs;
using InterviewAPI.Clients;
using InterviewAPI.DTOs.AiService;
using InterviewAPI.DTOs.Biometrics;
using InterviewAPI.DTOs.Evaluation;
using InterviewAPI.DTOs.Session;
using InterviewAPI.Helpers;
using InterviewAPI.Models;
using InterviewAPI.Repositories.Interfaces;
using InterviewAPI.Services.Interfaces;
using InterviewAPI.Services.Grading;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InterviewAPI.Services;

// Carries the HTTP status + machine-readable error code a session guard rejection should
// surface. Inherits InvalidOperationException so it is still caught the same as every other
// business-rule violation in this service if a controller action's catch (SessionGuardException)
// is ever missing — InterviewSessionController currently catches it explicitly on GenerateQuestion,
// SendMessage, ResumeSession, and Hint, reading StatusCode/ErrorCode/RetryAfterSeconds off it.
public class SessionGuardException : InvalidOperationException
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    // Only set for 429s (e.g. Skip cooldown) — how many seconds until the caller can retry.
    // Null for every other guard (403/409), which have no such window.
    public int? RetryAfterSeconds { get; }

    // Only set for "active_session_exists" (409) — the session the caller should be sent to
    // instead. Null for every other guard, which have no such session to redirect to.
    public Guid? ActiveSessionId { get; }

    public SessionGuardException(int statusCode, string errorCode, string message,
        int? retryAfterSeconds = null, Guid? activeSessionId = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        RetryAfterSeconds = retryAfterSeconds;
        ActiveSessionId = activeSessionId;
    }
}

public class SessionService : ISessionService
{

    // Second layer behind WebClient's own Room-entry integrity-policy check
    // (InterviewController.cs) — this one can't be bypassed by calling the API directly with a
    // valid JWT. Shared by every entry point that can hand question content back to the caller.
    private static void RequireIntegrityPolicyAccepted(InterviewSession session)
    {
        if (!session.IntegrityPolicyAccepted)
            throw new SessionGuardException(403, "integrity_policy_not_accepted",
                "You must accept the interview integrity policy before continuing.");
    }

    private static readonly int[] ValidDurations = { 15, 30, 45, 60 };
    private static readonly string[] ValidLevels = { "Intern", "Fresher", "Junior", "Middle", "Senior", "Lead" };
    private static readonly string[] ValidModes = { "Text", "Voice" };

    private static bool IsTerminalStatus(string status) =>
        status is "Completed" or "Cancelled" or "Expired";

    private async Task<EndResponse> BuildEndResponseAsync(
        InterviewSession session,
        CancellationToken ct,
        string message = "")
    {
        var hasSubstantiveAnswer = await _repo.HasSubstantiveAnswerAsync(session.SessionId, ct);
        var capabilities = SessionCapabilityPolicy.Evaluate(
            session.Status,
            session.TerminationReason,
            hasSubstantiveAnswer);
        return new EndResponse
        {
            Status = session.Status,
            EndedAt = session.EndedAt,
            TerminationReason = session.TerminationReason,
            CanGeneratePerformanceReport = capabilities.CanGeneratePerformanceReport,
            CanSubmitInterviewFeedback = capabilities.CanSubmitInterviewFeedback,
            Message = message
        };
    }

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

    // Self-healing check for an ongoing pause that has outlived the quota. There is no polling
    // loop: this runs at the top of the read paths a client can hit while paused —
    // ResumeSessionAsync (page load/F5), GetActiveSessionAsync (the Room's active-session lookup)
    // and GetTimerAsync — so the server, not the client tab, is what actually ends an expired
    // pause. Covers the case a client-side timer can't: the user closed the tab mid-pause and only
    // comes back after the quota would have run out — nothing client-side ran in the meantime, so
    // this is the only place that can catch it, on the next request that touches the session.
    //
    // Race safety: two overlapping callers (e.g. a reload racing an active-session lookup) can
    // both observe the same expired session before either writes. TryAutoResumeExpiredPauseAsync
    // is an atomic conditional UPDATE keyed on the exact PausedAt this caller read, so only the
    // first one to reach the database actually applies the resume; the loser's UPDATE affects zero
    // rows. Either way, the resumed state below is written back onto the caller's in-memory
    // `session` — no caller calls SaveChangesAsync afterwards, so this mutation can never be
    // persisted a second time.
    private async Task AutoResumeIfPauseExpiredAsync(InterviewSession session, CancellationToken ct)
    {
        if (session.Status != "Paused" || session.PausedAt is null)
            return;
        if (GetEffectivePausedSeconds(session) < MaxTotalPausedSeconds)
            return;

        var pausedAt = session.PausedAt.Value;
        var newTotalPausedSeconds = session.TotalPausedSeconds + (int)(VietnamTime.Now - pausedAt).TotalSeconds;
        await _repo.TryAutoResumeExpiredPauseAsync(session.SessionId, pausedAt, newTotalPausedSeconds, ct);

        session.Status = "InProgress";
        session.TotalPausedSeconds = newTotalPausedSeconds;
        session.PausedAt = null;
        session.UpdatedAt = VietnamTime.Now;
    }

    // Shared by SendMessageAsync (Step A.1, which turns a true return into a 409 the caller was
    // actively rejected by) and ResumeSessionAsync (a lazy read-path finalize for a session the
    // user abandoned and is only now coming back to — see report). Deliberately never throws:
    // the two callers react completely differently to "yes, this just got finalized," so that
    // decision belongs to them, not this helper. Only ever acts on "InProgress" — a Paused
    // session's elapsed time is frozen by GetEffectivePausedSeconds regardless of how long the
    // pause lasts, so there is nothing to expire there; that's SendMessageAsync's original
    // precondition, kept explicit here now that a caller without it exists.
    private async Task<bool> FinalizeIfTimeExpiredAsync(InterviewSession session, CancellationToken ct)
    {
        if (session.Status != "InProgress" || session.StartedAt is null)
            return false;

        var elapsed = (int)(VietnamTime.Now - session.StartedAt.Value).TotalSeconds - GetEffectivePausedSeconds(session);
        var remaining = session.DurationMinutes * 60 - elapsed;
        const int graceSeconds = 5;
        if (remaining > -graceSeconds)
            return false;

        var hasSubstantiveAnswer = await _repo.HasSubstantiveAnswerAsync(session.SessionId, ct);
        session.Status = hasSubstantiveAnswer ? "Completed" : "Expired";
        session.TerminationReason = hasSubstantiveAnswer ? null : SessionCapabilityPolicy.TimeExpired;
        session.EndedAt = VietnamTime.Now;
        session.UpdatedAt = VietnamTime.Now;

        var room = await _repo.GetRoomBySessionAsync(session.SessionId, ct);
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

        return true;
    }

    private readonly ISessionRepository _repo;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IPaymentApiClient _paymentApiClient;
    private readonly InterviewDbContext _db;
    private readonly ILogger<SessionService> _logger;
    private readonly IGradingQueue _gradingQueue;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IProfileBiometricsRepository _biometricsRepository;
    private readonly ISessionVerificationEventRepository _verificationEventRepository;
    private readonly IConfiguration _configuration;

    public SessionService(
        ISessionRepository repo,
        IAiServiceClient aiServiceClient,
        IPaymentApiClient paymentApiClient,
        InterviewDbContext db,
        ILogger<SessionService> logger,
        IGradingQueue gradingQueue,
        IHttpContextAccessor httpContextAccessor,
        IProfileBiometricsRepository biometricsRepository,
        ISessionVerificationEventRepository verificationEventRepository,
        IConfiguration configuration)
    {
        _repo = repo;
        _aiServiceClient = aiServiceClient;
        _paymentApiClient = paymentApiClient;
        _db = db;
        _logger = logger;
        _gradingQueue = gradingQueue;
        _httpContextAccessor = httpContextAccessor;
        _biometricsRepository = biometricsRepository;
        _verificationEventRepository = verificationEventRepository;
        _configuration = configuration;
    }

    // Biometric entry gate is active only when the feature flag is on — with the flag off,
    // sessions init and run exactly as before (guards below become no-ops).
    private bool BiometricsEnabled => _configuration.GetValue("Biometrics:Enabled", false);

    private async Task AttachVerificationCheckAsync(
        NextQuestionDto question, Guid sessionId, Guid userId, CancellationToken ct)
    {
        var check = await GetInSessionCheckAsync(
            userId, sessionId, question.SessionQuestionId, ct);
        question.VerificationCheckId = check.CheckId;
        question.VerificationCheckExpiresAt = check.ExpiresAt;
    }

    public async Task<InSessionCheckDueResponse> GetInSessionCheckAsync(
        Guid userId, Guid sessionId, Guid sessionQuestionId, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");
        if (!BiometricsEnabled || session.Status != "InProgress")
            return new InSessionCheckDueResponse();
        var sessionQuestion = await _db.SessionQuestions.AsNoTracking().FirstOrDefaultAsync(
            q => q.SessionQuestionId == sessionQuestionId && q.SessionId == sessionId, ct);
        if (sessionQuestion is null)
            throw new InvalidOperationException("Question not found in this session.");
        if (sessionQuestion.OrderIndex <= 1)
            return new InSessionCheckDueResponse();

        await SettleExpiredVerificationChecksAsync(session, ct);
        if (session.Status != "InProgress")
        {
            var capabilities = SessionCapabilityPolicy.Evaluate(
                session.Status,
                session.TerminationReason,
                await _repo.HasSubstantiveAnswerAsync(session.SessionId, ct));
            return new InSessionCheckDueResponse
            {
                Status = session.Status,
                TerminationReason = session.TerminationReason,
                CanGeneratePerformanceReport = capabilities.CanGeneratePerformanceReport,
                CanSubmitInterviewFeedback = capabilities.CanSubmitInterviewFeedback,
                ShouldEndSession = session.Status == "Cancelled",
                Message = session.Status == "Cancelled"
                    ? "Integrity warning limit reached. This interview has been ended."
                    : null
            };
        }

        var now = VietnamTime.Now;
        var existing = await _verificationEventRepository.GetQuestionCheckAsync(
            sessionId, sessionQuestionId, ct);
        if (existing is not null)
            return existing.Decision == "Pending"
                ? ToCheckDueResponse(existing, now, 0)
                : new InSessionCheckDueResponse();

        var ttlSeconds = _configuration.GetValue(
            "Biometrics:InSessionCheckTtlSeconds", 20);
        var check = new SessionVerificationEvent
        {
            SessionId = sessionId,
            UserId = userId,
            Phase = "InSession",
            Modality = "Check",
            Matched = false,
            Decision = "Pending",
            Detail = "{\"Reason\":\"QuestionCheckIssued\"}",
            IncidentId = Guid.NewGuid(),
            SessionQuestionId = sessionQuestionId,
            CheckSequence = 100000 + sessionQuestion.OrderIndex,
            ExpiresAt = now.AddSeconds(ttlSeconds),
            ChallengeJson = null,
            CreatedAt = now
        };
        if (await _verificationEventRepository.TryAddIncidentAsync(check, ct))
            return ToCheckDueResponse(check, now, 0);

        existing = await _verificationEventRepository.GetQuestionCheckAsync(
            sessionId, sessionQuestionId, ct);
        return existing?.Decision != "Pending"
            ? new InSessionCheckDueResponse()
            : ToCheckDueResponse(existing, now, 0);
    }

    private static InSessionCheckDueResponse ToCheckDueResponse(
        SessionVerificationEvent check, DateTime now, double targetMinutes) => new()
    {
        Due = true,
        ElapsedMinutes = Math.Max(0, (now - check.CreatedAt).TotalMinutes),
        TargetMinutes = targetMinutes,
        CheckId = check.IncidentId,
        ExpiresAt = check.ExpiresAt
    };

    private async Task SettleExpiredVerificationChecksAsync(
        InterviewSession session, CancellationToken ct)
    {
        if (!BiometricsEnabled || session.Status is not ("InProgress" or "Paused"))
            return;

        var expired = await _verificationEventRepository.ListExpiredPendingChecksAsync(
            session.SessionId, VietnamTime.Now, ct);
        var recorded = false;
        foreach (var check in expired)
            recorded |= await _verificationEventRepository.TryMarkCheckMissedAsync(check.Id, ct);
        if (!recorded)
            return;

        var events = await _verificationEventRepository.ListBySessionAsync(
            session.SessionId, "InSession", ct);
        var threshold = _configuration.GetValue("Biometrics:NoFaceStrikeThreshold", 3);
        var tabThreshold = _configuration.GetValue("Biometrics:TabStrikeThreshold", 3);
        var maxStrikes = _configuration.GetValue("Biometrics:InSessionMaxStrikes", 3);
        var strictness = _configuration.GetValue("Biometrics:InSessionStrictness", "Strict");
        var strikes = CheatingStrikeHelper.ComputeEffectiveStrikeCount(events, threshold, tabThreshold);
        // recorded is always true here (the method already returned above if nothing was marked
        // missed) — passed through explicitly so this reads as "a strike just happened", the same
        // shape as every other ShouldEndSession call site.
        if (!CheatingStrikeHelper.ShouldEndSession(recorded, strictness, strikes, maxStrikes))
            return;

        session.Status = "Cancelled";
        session.TerminationReason = SessionCapabilityPolicy.IntegrityViolation;
        session.EndedAt = VietnamTime.Now;
        session.UpdatedAt = VietnamTime.Now;
        var room = await _repo.GetRoomBySessionAsync(session.SessionId, ct);
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
    }

    public async Task<ConfigResponse> ConfigDurationAsync(Guid userId, ConfigDurationRequest req, CancellationToken ct)
    {
        // DEMO-SESSION: remove after defence — start
        // ValidDurations is a hard whitelist, so admitting the demo duration means touching this
        // check — the one place demo logic sits on a normal session's path. Written as a guard in
        // FRONT of the original check rather than an extra `&&` clause inside it, so that deleting
        // everything between these two markers leaves the original statement intact and correct:
        //     if (!ValidDurations.Contains(req.DurationMinutes))
        //         throw new InvalidOperationException(...);
        // IsDemoDuration is false whenever DEMO_SESSION_ENABLED is off, so with the flag off a
        // 5-minute request falls through to the whitelist and is rejected exactly as before.
        if (!DemoSession.IsDemoDuration(_configuration, req.DurationMinutes))
        // DEMO-SESSION: remove after defence — end
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

        // Step A.1: Biometric entry gate — a face enrollment is required before a session can
        // be created at all (server-side, so calling the API directly cannot bypass the UI).
        // TODO (future backlog): award a one-time credit bonus on first biometric enrollment.
        if (BiometricsEnabled)
        {
            var faceRow = await _biometricsRepository.GetAsync(
                userId, ProfileBiometricsService.ModalityFace, ct);
            if (faceRow is null)
                throw new InvalidOperationException(
                    "Please enroll your face before starting an interview.");
        }

        // Step B: TODO — deduct credit via PaymentApiClient (PaymentAPI not implemented yet).
        // const bool CreditsEnabled = false;
        // if (CreditsEnabled) await _paymentClient.DeductCreditAsync(userId, ct);

        // Step B.1: Persist the chosen role on the session (used by the plan below and shown later
        // in History/Report). Falls back to the existing/default when the caller omits it.
        if (!string.IsNullOrWhiteSpace(req.Position))
            session.Position = req.Position;
        if (req.JdId.HasValue)
            session.JdId = req.JdId;

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

        // ai-service is a hard dependency here: without a question set there is no interview to
        // start. Translate any failure into SessionInitDependencyException so the controller can
        // answer 503 ("try again shortly") instead of letting a raw dependency error surface as a
        // 400, which would tell the user their request was malformed when it was not.
        AiQuestionPlanResponse? plan;
        try
        {
            plan = await _aiServiceClient.PlanQuestionsAsync(planRequest, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "InitAsync: ai-service PlanQuestions failed for session {SessionId}; session was not initialized.",
                session.SessionId);
            throw new SessionInitDependencyException(ex);
        }

        // DEMO-SESSION: remove after defence — start
        // Keep only the first DemoSession.QuestionCount questions of the real RAG plan. ai-service
        // still runs its full pipeline for a 5-minute request (target_questions(5) => 8 questions)
        // — nothing here is scripted, we simply stop after the first few.
        //
        // Placement is deliberate: this runs BEFORE Step D maps AiPlannedQuestion -> SessionQuestion
        // and therefore before Step E's single INSERT. The discarded questions never become
        // entities and never reach the database, so a demo session leaves exactly 3 rows in
        // SessionQuestions with no orphans for anyone inspecting the data afterwards.
        //
        // Taking the FIRST 3 (rather than sampling across tracks) is intentional: ai-service's
        // _weave orders the pool so any prefix is valid — see its comment "a time-driven session
        // may stop anywhere" — and every question is planned at the same difficulty (the
        // candidate's level), so a prefix is not an easier subset. The first three come from the
        // core technical track; Problem-Solving first appears at question 4.
        if (plan is not null
            && DemoSession.IsDemoDuration(_configuration, session.DurationMinutes)
            && plan.Questions.Count > DemoSession.QuestionCount)
        {
            var plannedCount = plan.Questions.Count;
            plan.Questions = plan.Questions.Take(DemoSession.QuestionCount).ToList();
            _logger.LogInformation(
                "DEMO-SESSION: trimmed ai-service plan from {PlannedCount} to {KeptCount} questions " +
                "for session {SessionId} before persistence.",
                plannedCount, plan.Questions.Count, session.SessionId);
        }
        // DEMO-SESSION: remove after defence — end

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

        // Step F: Transition session to Initialized state. StartedAt is NOT set here — the user
        // still has to get through Verify (face match, up to 3 attempts) and IntegrityPolicy
        // (read 5 rules) before ever seeing a question, ~20s measured in practice. It's set in
        // GenerateQuestionAsync's Step D.1 instead, at the actual Initialized→InProgress
        // transition, so the clock starts when the interview truly begins.
        session.Status = "Initialized";
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

        // Step A.1: Biometric entry gate (server-enforced). The session must hold at least one
        // passed Entry/Face verification event BEFORE any question is served — and therefore
        // before the Initialized→InProgress transition and the credit deduction below. Calling
        // this API directly without passing the Verify screen is rejected here.
        if (BiometricsEnabled)
        {
            var entryEvents = await _verificationEventRepository.ListBySessionAsync(sessionId, "Entry", ct);
            var facePassed = entryEvents.Any(
                e => e.Modality == ProfileBiometricsService.ModalityFace && e.Decision == "Pass");
            if (!facePassed)
                throw new InvalidOperationException("Identity verification required before starting the interview.");
        }

        // Step A.2: Integrity policy gate — must be accepted before any question content is
        // served or credit is deducted (Step D.1 below). Second layer behind WebClient's Room
        // entry check; this one can't be bypassed by calling the API directly.
        RequireIntegrityPolicyAccepted(session);

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

                // PaymentAPI never answered: its Message holds transport detail (host names, ports)
                // that must not reach the user. The log line above already captured it.
                if (!deductResult.ServiceAvailable)
                    throw new CreditServiceUnavailableException();

                // PaymentAPI answered and refused — e.g. "Insufficient credits". That message is
                // written for the user and stays intact.
                throw new InvalidOperationException($"Credit deduction failed: {deductResult.Message}");
            }

            // Update session with credit cost and the real interview start time — this IS the
            // Initialized→InProgress transition, so StartedAt belongs here rather than at Init
            // (see InitAsync Step F: Verify + IntegrityPolicy can take ~20s before this point).
            var refreshedSession = await _repo.GetByIdAsync(sessionId, ct);
            if (refreshedSession is not null)
            {
                session = refreshedSession;
                session.CreditCost = creditCost;
                session.StartedAt = VietnamTime.Now;
                await _repo.SaveChangesAsync(ct);
            }
        }

        // Step E: Lưu tin nhắn câu hỏi của AI vào ChatMessages.
        // Defensive idempotency guard: this endpoint is normally called once per session (at
        // creation, from StartInterviewAsync), but a duplicate/racing call would otherwise
        // insert a second identical AI Question message — skip the insert if one already exists
        // for this question. Does not touch the credit/refund logic below in any way.
        var alreadyAsked = await _db.ChatMessages.AnyAsync(
            m => m.SessionQuestionId == nextQuestion.SessionQuestionId
              && m.SenderType == "AI"
              && m.MessageType == "Question",
            ct);
        try
        {
            if (!alreadyAsked)
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
        await SettleExpiredVerificationChecksAsync(session, ct);
        if (session.Status != "InProgress")
            throw new InvalidOperationException("Session is not in progress.");

        // Step A.1: Server-side deadline enforcement. The countdown shown to the user runs
        // entirely client-side (interview-room.js's setInterval) — nothing before this point
        // ever checked whether the session's time actually ran out, so a direct API call
        // (Postman, or DevTools with the timer paused) could keep answering indefinitely.
        // FinalizeIfTimeExpiredAsync both decides AND finalizes (Completed/Expired, room closed)
        // in one step — this caller is the one that turns "yes, expired" into a hard rejection;
        // ResumeSessionAsync reuses the same helper but reacts differently (see its own comment).
        if (await FinalizeIfTimeExpiredAsync(session, ct))
            throw new SessionGuardException(409, "session_time_expired",
                "This interview's time has run out.");

        // Step A.2: Integrity policy gate — placed before Step B and before both idempotent
        // early-return branches below (existence guard, claim guard) so it applies uniformly on
        // every path that can hand back question content, including duplicate/race retries.
        RequireIntegrityPolicyAccepted(session);

        // Step A.3: Skip-spam cooldown. Only "Skipped" is a user-clickable action that can be
        // spammed — real answers and the server-verified "TimedOut" auto-advance are exempt.
        // Checked before Step B/B.1/B.2 so a cooldown rejection costs nothing (no question
        // lookup, no AnsweredQuestions claim, no ChatMessage) and needs no rollback.
        if (req.Reason == "Skipped")
        {
            var cooldownSeconds = _configuration.GetValue("Interview:SkipCooldownSeconds", 10);
            var (allowed, retryAfterSeconds) = await _repo.TryConsumeSkipCooldownAsync(sessionId, cooldownSeconds, ct);
            if (!allowed)
                throw new SessionGuardException(429, "skip_cooldown",
                    $"Please wait {retryAfterSeconds}s before skipping again.", retryAfterSeconds);
        }

        // Step B: Kiểm tra câu hỏi thuộc phiên này.
        var currentQuestion = await _repo.GetQuestionByIdAsync(req.SessionQuestionId, ct);
        if (currentQuestion is null || currentQuestion.SessionId != sessionId)
            throw new InvalidOperationException("Question not found.");

        // Content filter — runs BEFORE the Step B.1 idempotency claim below on purpose: a
        // rejected answer must not consume the turn (no claim, no counter bump, no ChatMessage).
        // Skip/auto-advance turns carry empty content (reason Skipped/TimedOut) and are not real
        // answers, so they bypass the filter. The client mirrors this check for instant feedback
        // (content-filter.js), but THIS is the enforcement a direct API call cannot dodge.
        if (req.Reason != "Skipped" && !string.IsNullOrWhiteSpace(req.Content)
            && !ContentModerationHelper.IsAcceptable(req.Content))
        {
            throw new InvalidOperationException(
                "Your answer appears to contain inappropriate or unreadable content. Please review it and try again.");
        }

        // Step B.1: Per-question existence guard. Catches duplicate submissions that arrive with
        // enough of a gap that each one reads a fresh (already-advanced) AnsweredQuestions counter
        // — e.g. several idle-timer ticks firing in a burst (see interview-room.js autoAdvance).
        // The counter-based claim below only serializes requests that read the SAME counter value
        // at the SAME instant (true concurrency); it has no memory of "was THIS SessionQuestionId
        // already answered," so a request arriving after an earlier duplicate has already
        // completed would otherwise sail through and re-insert. Real-data evidence (2026-07-24):
        // 5 near-simultaneous auto-advance retries all succeeded and inserted 5 ChatMessages for
        // the same question because each read the counter fresh.
        if (await _repo.HasUserAnswerAsync(req.SessionQuestionId, ct))
        {
            _logger.LogInformation(
                "SendMessage: question {SessionQuestionId} already has a User/Answer message — no-op, returning current state.",
                req.SessionQuestionId);
            return await BuildCurrentTurnResponseAsync(sessionId, currentQuestion.OrderIndex, ct);
        }

        // Step B.2: Atomic idempotency guard (per-turn). Claims this turn by incrementing
        // AnsweredQuestions only if it still equals what we just read — same conditional-UPDATE
        // pattern as UpdateStatusIfAsync uses for credit. Handles TRUE concurrent duplicates (same
        // question, requests landing at the exact same instant) that the existence check above
        // can race past (both could read "not answered yet" before either commits) — this claim
        // only lets one of them through regardless.
        bool claimed = await _repo.IncrementAnsweredIfAsync(sessionId, session.AnsweredQuestions, ct);
        if (!claimed)
        {
            _logger.LogInformation(
                "SendMessage: duplicate/racing call for session {SessionId}, question {SessionQuestionId} — no-op, returning current state.",
                sessionId, req.SessionQuestionId);
            return await BuildCurrentTurnResponseAsync(sessionId, currentQuestion.OrderIndex, ct);
        }

        var answerPersisted = false;
        try
        {
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

            // Step D: Persist the user's answer before offering substantive work to the grading queue.
            var chatMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                SessionQuestionId = req.SessionQuestionId,
                SenderType = SubstantiveAnswerRules.UserSenderType,
                MessageType = SubstantiveAnswerRules.AnswerMessageType,
                Content = req.Content,
                ResponseTimeMs = req.ResponseTimeMs,
                AnswerReason = answerReason,
                SentAt = VietnamTime.Now
            };
            await _repo.AddChatMessageAsync(chatMessage, ct);
            answerPersisted = true;

            // Step E: AnsweredQuestions was incremented atomically by the claim above.
            // Queue acceptance deliberately does not observe RequestAborted after persistence:
            // ordinary navigation must not strand a saved substantive answer without grading work.
            if (SubstantiveAnswerRules.IsSubstantive(chatMessage))
            {
                var bearerToken = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
                var workItem = new GradingWorkItem(
                    sessionId,
                    currentQuestion.SessionQuestionId,
                    chatMessage.MessageId,
                    userId,
                    new EvaluateAnswerPayload
                    {
                        Question = currentQuestion.Content,
                        ReferenceAnswer = currentQuestion.ReferenceAnswer ?? string.Empty,
                        Category = string.Empty,
                        Difficulty = string.Empty,
                        UserAnswer = chatMessage.Content
                    },
                    bearerToken);

                try
                {
                    await _gradingQueue.EnqueueAsync(workItem);
                }
                catch (Exception ex)
                {
                    // The ChatMessage and counter now form one persisted answer. Shutdown may prevent
                    // in-memory acceptance, but it must never roll back only the counter or fabricate data.
                    _logger.LogWarning(
                        ex,
                        "Grading work could not be accepted after answer persistence for session {SessionId}, " +
                        "question {SessionQuestionId}, message {ChatMessageId}, user {UserId}.",
                        sessionId,
                        currentQuestion.SessionQuestionId,
                        chatMessage.MessageId,
                        userId);
                }
            }
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
                var response = new MessageResponse
                {
                    Ended = false,
                    // session.AnsweredQuestions is the PRE-claim value read at Step A; the claim
                    // above (IncrementAnsweredIfAsync) only succeeds when the DB still matches
                    // that exact value, so "+1" is the confirmed new value — no need to re-read
                    // (GetByIdAsync is tracking and would return the SAME stale in-memory entity;
                    // ExecuteUpdateAsync bypasses the change tracker and never refreshes it).
                    AnsweredQuestions = session.AnsweredQuestions + 1,
                    TotalQuestions = session.TotalQuestions,
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
                await AttachVerificationCheckAsync(response.NextQuestion, sessionId, userId, ct);
                return response;
            }

            // Step H: Đã trả lời hết — đóng phiên và phòng phỏng vấn.
            var hasSubstantiveAnswer = await _repo.HasSubstantiveAnswerAsync(sessionId, ct);
            session.Status = hasSubstantiveAnswer ? "Completed" : "Expired";
            session.TerminationReason = hasSubstantiveAnswer ? null : SessionCapabilityPolicy.TimeExpired;
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

            var capabilities = SessionCapabilityPolicy.Evaluate(
                session.Status, session.TerminationReason, hasSubstantiveAnswer);
            return new MessageResponse
            {
                Ended = true,
                Status = session.Status,
                TerminationReason = session.TerminationReason,
                CanGeneratePerformanceReport = capabilities.CanGeneratePerformanceReport,
                CanSubmitInterviewFeedback = capabilities.CanSubmitInterviewFeedback,
                Message = "This interview has ended.",
                NextQuestion = null,
                // Same reasoning as the Step G response above: session.AnsweredQuestions is the
                // pre-claim Step A snapshot (ExecuteUpdateAsync never refreshes it), so the
                // confirmed post-claim value is +1, not the raw field.
                AnsweredQuestions = session.AnsweredQuestions + 1,
                TotalQuestions = session.TotalQuestions
            };
        }
        catch
        {
            if (!answerPersisted)
            {
                // The claim did not produce a persisted answer, so a retry must be able to reclaim it.
                await _repo.DecrementAnsweredAsync(sessionId, ct);
            }
            else
            {
                _logger.LogWarning(
                    "Answer processing failed after persistence for session {SessionId}, question {SessionQuestionId}; " +
                    "the AnsweredQuestions claim remains aligned with the saved ChatMessage.",
                    sessionId,
                    req.SessionQuestionId);
            }

            throw;
        }
    }

    // Read-only reconstruction of "what SendMessageAsync's response would be right now" for a
    // duplicate/racing call that lost the idempotency claim — mirrors Step F/G/H's response
    // shape without inserting anything, so the loser sees the SAME state the winner produced.
    private async Task<MessageResponse> BuildCurrentTurnResponseAsync(Guid sessionId, int afterOrderIndex, CancellationToken ct)
    {
        // AsNoTracking + a fresh query here on purpose: this method runs in a request that
        // already tracked an InterviewSession earlier at Step A (via _repo.GetByIdAsync). A
        // second tracked read for the SAME id would return that same cached in-memory instance
        // instead of the winner's committed update (ExecuteUpdateAsync never refreshes tracked
        // entities), which is exactly the bug this method exists to report around.
        var session = await _db.InterviewSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && !s.IsDeleted, ct);
        var nextQuestion = await _repo.GetNextUnansweredQuestionAsync(sessionId, afterOrderIndex, ct);

        if (nextQuestion is null)
        {
            if (session is null)
                return new MessageResponse { Ended = true, Message = "This interview has ended." };

            var hasSubstantiveAnswer = await _repo.HasSubstantiveAnswerAsync(sessionId, ct);
            var capabilities = SessionCapabilityPolicy.Evaluate(
                session.Status, session.TerminationReason, hasSubstantiveAnswer);
            return new MessageResponse
            {
                Ended = true,
                Status = session.Status,
                TerminationReason = session.TerminationReason,
                CanGeneratePerformanceReport = capabilities.CanGeneratePerformanceReport,
                CanSubmitInterviewFeedback = capabilities.CanSubmitInterviewFeedback,
                Message = "This interview has ended.",
                NextQuestion = null,
                AnsweredQuestions = session.AnsweredQuestions,
                TotalQuestions = session.TotalQuestions
            };
        }

        var (reminderSeconds, hardSeconds) = GetIdleThresholds(null);
        var elapsedSeconds = nextQuestion.AskedAt is null || session is null
            ? 0
            : Math.Max(0, (int)(VietnamTime.Now - nextQuestion.AskedAt.Value).TotalSeconds - GetEffectivePausedSeconds(session));
        return new MessageResponse
        {
            Ended = false,
            AnsweredQuestions = session?.AnsweredQuestions ?? 0,
            TotalQuestions = session?.TotalQuestions ?? 0,
            NextQuestion = new NextQuestionDto
            {
                SessionQuestionId = nextQuestion.SessionQuestionId,
                OrderIndex = nextQuestion.OrderIndex,
                Content = nextQuestion.Content,
                IdleReminderSeconds = reminderSeconds,
                IdleHardSeconds = hardSeconds,
                IdleElapsedSeconds = elapsedSeconds
            }
        };
    }

    public async Task<TimerResponse> GetTimerAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");

        await AutoResumeIfPauseExpiredAsync(session, ct);
        await SettleExpiredVerificationChecksAsync(session, ct);

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

    public async Task<ActiveSessionResponse?> GetActiveSessionAsync(Guid userId, CancellationToken ct)
    {
        var session = await _repo.GetActiveSessionAsync(userId, ct);
        if (session is null)
            return null;

        // Same lazy-finalize sequence as GetTimerAsync/ResumeSessionAsync: pause auto-resume
        // first (it can change TotalPausedSeconds/PausedAt, which the elapsed-time formula below
        // depends on), then the time-expired check. A session that just got finalized here is no
        // longer "active" — report it the same as if there were none.
        await AutoResumeIfPauseExpiredAsync(session, ct);
        if (await FinalizeIfTimeExpiredAsync(session, ct))
            return null;

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

        return new ActiveSessionResponse
        {
            SessionId = session.SessionId,
            Status = session.Status,
            StartedAt = session.StartedAt,
            DurationMinutes = session.DurationMinutes,
            RemainingSeconds = remaining
        };
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
            return await BuildEndResponseAsync(session, ct);
        if (session.Status != "InProgress")
            throw new InvalidOperationException("Session cannot complete from its current state.");

        var elapsedSeconds = session.StartedAt is null
            ? 0
            : (VietnamTime.Now - session.StartedAt.Value).TotalSeconds - GetEffectivePausedSeconds(session);
        if (session.StartedAt is null || elapsedSeconds < session.DurationMinutes * 60)
            throw new InvalidOperationException("Session cannot complete before its deadline.");

        var room = await _repo.GetRoomBySessionAsync(sessionId, ct);
        var hasSubstantiveAnswer = await _repo.HasSubstantiveAnswerAsync(sessionId, ct);
        session.Status = hasSubstantiveAnswer ? "Completed" : "Expired";
        session.TerminationReason = hasSubstantiveAnswer
            ? null
            : SessionCapabilityPolicy.TimeExpired;
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

        return await BuildEndResponseAsync(session, ct, "This interview has ended.");
    }

    public async Task<EndResponse> EndSessionAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");
        if (IsTerminalStatus(session.Status))
            return await BuildEndResponseAsync(session, ct);
        if (session.Status is not ("Initialized" or "InProgress" or "Paused"))
            throw new InvalidOperationException("Session cannot be cancelled from its current state.");

        session.Status = "Cancelled";
        session.TerminationReason = SessionCapabilityPolicy.CandidateCancelled;

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

        return await BuildEndResponseAsync(session, ct, "This interview has been cancelled.");
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
            PausedSecondsRemaining = Math.Max(0, MaxTotalPausedSeconds - GetEffectivePausedSeconds(session))
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
            PausedSecondsRemaining = Math.Max(0, MaxTotalPausedSeconds - GetEffectivePausedSeconds(session))
        };
    }

    public async Task<ReportCameraViolationResponse> ReportCameraViolationAsync(
        Guid userId, Guid sessionId, ReportCameraViolationRequest req, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");
        if (session.Status is not ("InProgress" or "Paused"))
            throw new InvalidOperationException("Session is not active.");
        if (req.IncidentId == Guid.Empty)
            throw new InvalidOperationException("IncidentId is required.");
        if (req.Reason is not ("PermissionDenied" or "Disconnected"))
            throw new InvalidOperationException("Reason must be PermissionDenied or Disconnected.");

        var maxStrikes = _configuration.GetValue("Biometrics:InSessionMaxStrikes", 3);
        var strictness = _configuration.GetValue("Biometrics:InSessionStrictness", "Strict");
        var noFaceStrikeThreshold = _configuration.GetValue("Biometrics:NoFaceStrikeThreshold", 3);
        var tabStrikeThreshold = _configuration.GetValue("Biometrics:TabStrikeThreshold", 3);

        _logger.LogInformation(
            "ReportCameraViolation: session {SessionId}, incidentId={IncidentId}, reason={Reason}",
            sessionId, req.IncidentId, req.Reason);

        var inserted = await _verificationEventRepository.TryAddIncidentAsync(
            new SessionVerificationEvent
            {
                SessionId = sessionId,
                UserId = userId,
                Phase = "InSession",
                Modality = "Camera",
                Matched = false,
                Decision = "Flag",
                IncidentId = req.IncidentId,
                Detail = JsonSerializer.Serialize(new { req.Reason }),
                CreatedAt = VietnamTime.Now,
            },
            ct);

        var count = inserted
            ? await _repo.IncrementCameraViolationAsync(sessionId, ct)
            : await _db.InterviewSessions.AsNoTracking()
                .Where(s => s.SessionId == sessionId && !s.IsDeleted)
                .Select(s => s.CameraViolationCount)
                .FirstOrDefaultAsync(ct);

        var events = await _verificationEventRepository.ListBySessionAsync(sessionId, "InSession", ct);
        var effectiveStrikeCount = CheatingStrikeHelper.ComputeEffectiveStrikeCount(
            events, noFaceStrikeThreshold, tabStrikeThreshold);
        var displayStrikeCount = Math.Min(effectiveStrikeCount, maxStrikes);
        var shouldEndSession = CheatingStrikeHelper.ShouldEndSession(
            inserted, strictness, effectiveStrikeCount, maxStrikes);

        var message = req.Reason == "PermissionDenied"
            ? "Camera access was not restored within the allowed time."
            : "The camera was not reconnected within the allowed time.";

        if (shouldEndSession)
        {
            session.Status = "Cancelled";
            session.TerminationReason = SessionCapabilityPolicy.IntegrityViolation;
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

            message = $"Integrity warning limit reached ({displayStrikeCount}/{maxStrikes}). " +
                      "This interview has been ended.";
        }

        var capabilities = SessionCapabilityPolicy.Evaluate(
            session.Status,
            session.TerminationReason,
            await _repo.HasSubstantiveAnswerAsync(session.SessionId, ct));
        return new ReportCameraViolationResponse
        {
            Status = session.Status,
            TerminationReason = session.TerminationReason,
            CanGeneratePerformanceReport = capabilities.CanGeneratePerformanceReport,
            CanSubmitInterviewFeedback = capabilities.CanSubmitInterviewFeedback,
            CameraViolationCount = count,
            StrikeCount = displayStrikeCount,
            MaxStrikes = maxStrikes,
            Duplicate = !inserted,
            ShouldEndSession = shouldEndSession,
            Message = message,
        };
    }

    public async Task<ResumeResponse> ResumeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetWithFullHistoryAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new InvalidOperationException("Session not found.");

        await AutoResumeIfPauseExpiredAsync(session, ct);
        // Lazy finalize for a session abandoned mid-interview (tab closed, power loss, network
        // drop — nothing background ever scans for this, see report) — runs AFTER the pause
        // auto-resume above because that call can change TotalPausedSeconds/PausedAt, which the
        // elapsed-time formula inside FinalizeIfTimeExpiredAsync depends on. Unlike
        // SendMessageAsync, Resume must NEVER throw here — the user coming back just needs to
        // see "this interview has ended," not an error; the rest of this method already treats
        // a non-InProgress/Paused Status as "no current question" further down.
        await FinalizeIfTimeExpiredAsync(session, ct);
        await SettleExpiredVerificationChecksAsync(session, ct);

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
        // Defense in depth: only for a session that is still active. A session ended EARLY (e.g.
        // "End Interview" while questions remain unanswered) still has a real unanswered
        // SessionQuestion row — without this guard this would keep handing back a "live" current
        // question for a session that has already ended, letting a stale/bfcache'd Room page look
        // interactive again. This method intentionally does NOT throw for a terminal status (it
        // still needs to succeed and report Status truthfully — see InterviewController.Room's
        // own status check, which is the primary guard and decides where to redirect).
        //
        // Also excludes "Initialized" and !IntegrityPolicyAccepted: Resume itself must NEVER
        // throw on status/policy — InterviewController.Room calls it as its status source BEFORE
        // the entry gate and the integrity-policy redirect even run, so a throw here would strand
        // every fresh session (see report). Question content only leaks once the session has
        // actually started (GenerateQuestionAsync ran — credit charged, entry gate + policy both
        // already enforced there); everything else about the response (Status, timers, chat
        // history, ...) still returns normally so the Room controller can keep reading state.
        NextQuestionDto? currentQuestion = null;
        if (session.Status is "InProgress" or "Paused" && session.IntegrityPolicyAccepted)
        {
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
        }

        // Room UX Phase 3 — true once this user has ever dismissed/completed the onboarding tour
        // (no row yet = never seen it). This is a nice-to-have onboarding flag, never allowed to
        // block entering the room — if the lookup fails for any reason (e.g. table not migrated
        // yet on this environment), fall back to "not seen" so the tour just shows once more
        // instead of taking down Resume, which is on the critical path to the interview itself.
        if (currentQuestion is not null)
            await AttachVerificationCheckAsync(currentQuestion, sessionId, userId, ct);

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
            PausedSecondsRemaining = Math.Max(0, MaxTotalPausedSeconds - GetEffectivePausedSeconds(session)),
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
        var questionTypeOptions = await _repo.GetHistoryQuestionTypeOptionsAsync(userId, ct);

        return new SessionHistoryResult
        {
            Items = sessions.Select(s => new SessionHistoryItemDto
            {
                SessionId = s.SessionId,
                // Title column was removed from the schema — DTO field stays unset (null);
                // clients fall back to Position for display.
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
            PositionOptions = positionOptions,
            QuestionTypeOptions = questionTypeOptions
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
                SessionQuestionId = q.SessionQuestionId,
                QuestionIndex = q.OrderIndex,
                QuestionText = q.Content,
                HasSubstantiveAnswer = q.ChatMessages.Any(SubstantiveAnswerRules.IsSubstantive),
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
                        ResponseDurationMs = m.ResponseTimeMs,
                        AnswerReason = m.AnswerReason,
                        IsSubstantiveAnswer = SubstantiveAnswerRules.IsSubstantive(m)
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
        var count = await _repo.CountSystemFeedbackQualifyingSessionsAsync(userId, ct);
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

        var hasSubstantiveAnswer = await _repo.HasSubstantiveAnswerAsync(session.SessionId, ct);

        var capabilities = SessionCapabilityPolicy.Evaluate(
            session.Status,
            session.TerminationReason,
            hasSubstantiveAnswer);
        return new SessionFeedbackEligibilityResponse
        {
            SessionId = session.SessionId,
            Status = session.Status,
            TerminationReason = session.TerminationReason,
            HasSubstantiveAnswer = hasSubstantiveAnswer,
            CanSubmitInterviewFeedback = capabilities.CanSubmitInterviewFeedback,
            CanGeneratePerformanceReport = capabilities.CanGeneratePerformanceReport
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
            .OrderBy(q => q.OrderIndex)
            .ThenBy(q => q.SessionQuestionId)
            .Select(q => new TranscriptQuestionDto
            {
                SessionQuestionId = q.SessionQuestionId,
                OrderIndex = q.OrderIndex,
                Content = q.Content,
                HasSubstantiveAnswer = q.ChatMessages.Any(SubstantiveAnswerRules.IsSubstantive),
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
            TerminationReason = session.TerminationReason,
            DurationMinutes = session.DurationMinutes,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            TotalPausedSeconds = session.TotalPausedSeconds,
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
        {
            // Only blocks brand-new sessions — a session still being configured (Status=
            // "Configured", the branch below) is untouched. Calls the SERVICE-level
            // GetActiveSessionAsync (not _repo.GetActiveSessionAsync directly): it already runs
            // FinalizeIfTimeExpiredAsync before returning, so a session whose time already ran
            // out gets closed out and reported as null here instead of wrongly blocking a new one.
            var activeSession = await GetActiveSessionAsync(userId, ct);
            if (activeSession is not null)
                throw new SessionGuardException(409, "active_session_exists",
                    "You already have an interview in progress.",
                    activeSessionId: activeSession.SessionId);

            return await _repo.CreateAsync(factory(), ct);
        }

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

    // How long after one re-queue burst before the same session may trigger another.
    private const int RegradeCooldownSeconds = 60;

    // Last time each session was allowed a re-queue burst.
    //
    // WHAT THIS PROTECTS AGAINST: a user refreshing the report page in a loop. Without it, every
    // refresh would enqueue one job per missing answer, stacking duplicate work that all competes
    // for the same GPU. EvaluateAnswerAsync is idempotent at the ROW level, which prevents
    // duplicate evaluations — it does nothing to prevent duplicate QUEUE ENTRIES.
    //
    // WHAT THIS DOES *NOT* PROTECT AGAINST — do not assume more of it than it does:
    //   - It is IN-MEMORY. A restart clears it, so the first request after a restart always
    //     passes, regardless of how recently a burst ran.
    //   - It is PER-INSTANCE. Running more than one InterviewAPI replica gives each its own copy,
    //     so N replicas allow N concurrent bursts. Fine at this project's single-instance scale;
    //     a shared store (or moving the guard into the database) is the fix if that ever changes.
    //   - It is NOT a security control. The endpoint is reachable by any authenticated owner of
    //     the session through the gateway — nothing about the network topology restricts it, and
    //     the gateway's "internal" path filter does not cover this route. This cooldown is the
    //     ONLY thing rate-limiting the recovery path. Do not remove it on the assumption that
    //     network isolation is doing the work, because it is not.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, DateTime> RegradeCooldowns = new();

    public async Task<RequeueMissingEvaluationsResponse> RequeueMissingEvaluationsAsync(
        Guid userId, Guid sessionId, RequeueMissingEvaluationsRequest req, CancellationToken ct)
    {
        var session = await _repo.GetWithFullHistoryAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new KeyNotFoundException("Session not found.");

        var requestedIds = (req.SessionQuestionIds ?? new List<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var response = new RequeueMissingEvaluationsResponse
        {
            SessionId = sessionId,
            RequestedCount = requestedIds.Count
        };

        if (requestedIds.Count == 0)
            return response;

        // Cooldown check comes before any work is built, so a suppressed burst costs one dictionary
        // read. Logged at Warning: a suppressed recovery is a recovery that did NOT happen, and the
        // whole point of this path is that silent drops are what got us here.
        var now = VietnamTime.Now;
        if (RegradeCooldowns.TryGetValue(sessionId, out var lastBurst))
        {
            var elapsed = (now - lastBurst).TotalSeconds;
            if (elapsed < RegradeCooldownSeconds)
            {
                response.CooldownActive = true;
                response.CooldownSecondsRemaining = (int)Math.Ceiling(RegradeCooldownSeconds - elapsed);
                _logger.LogWarning(
                    "Re-queue of {RequestedCount} missing evaluation(s) SUPPRESSED by cooldown for " +
                    "session {SessionId}, user {UserId}. {SecondsRemaining}s remaining of the " +
                    "{CooldownSeconds}s window. Nothing was queued by this call.",
                    requestedIds.Count,
                    sessionId,
                    userId,
                    response.CooldownSecondsRemaining,
                    RegradeCooldownSeconds);
                return response;
            }
        }

        var bearerToken = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        var questionsById = session.SessionQuestions.ToDictionary(q => q.SessionQuestionId);

        foreach (var questionId in requestedIds)
        {
            if (!questionsById.TryGetValue(questionId, out var question))
            {
                response.NotEligibleCount++;
                continue;
            }

            // Same substantive-answer rule the original enqueue used, so a skipped or timed-out
            // question is never re-queued: those legitimately have no evaluation and the aggregate
            // report already excludes them.
            var answer = question.ChatMessages
                .Where(SubstantiveAnswerRules.IsSubstantive)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefault();

            if (answer is null)
            {
                response.NotEligibleCount++;
                continue;
            }

            var workItem = new GradingWorkItem(
                sessionId,
                question.SessionQuestionId,
                answer.MessageId,
                userId,
                new EvaluateAnswerPayload
                {
                    Question = question.Content,
                    ReferenceAnswer = question.ReferenceAnswer ?? string.Empty,
                    Category = string.Empty,
                    Difficulty = string.Empty,
                    UserAnswer = answer.Content
                },
                bearerToken);

            // TryEnqueue, NOT EnqueueAsync: this runs inside a live HTTP request and must never
            // block on a full queue. See IGradingQueue for why the two paths differ.
            if (_gradingQueue.TryEnqueue(workItem))
                response.EnqueuedCount++;
            else
                response.DroppedCount++;
        }

        var eligibleCount = response.EnqueuedCount + response.DroppedCount;
        if (eligibleCount > 0)
        {
            // Start the cooldown on any burst that had real work to do — including one whose items
            // were all dropped. A full queue means the system is already saturated, which is
            // precisely when an immediate retry is least welcome.
            RegradeCooldowns[sessionId] = now;
            PruneRegradeCooldowns(now);
        }

        if (response.DroppedCount > 0)
        {
            _logger.LogWarning(
                "Re-queue for session {SessionId}, user {UserId}: {EnqueuedCount} queued but " +
                "{DroppedCount} DROPPED because the grading queue was full or shutting down. " +
                "Those answers stay ungraded until the next attempt after the {CooldownSeconds}s cooldown.",
                sessionId, userId, response.EnqueuedCount, response.DroppedCount, RegradeCooldownSeconds);
        }
        else
        {
            _logger.LogInformation(
                "Re-queue for session {SessionId}, user {UserId}: {EnqueuedCount} of {RequestedCount} " +
                "requested evaluation(s) queued; {NotEligibleCount} had no substantive answer to grade.",
                sessionId, userId, response.EnqueuedCount, response.RequestedCount, response.NotEligibleCount);
        }

        return response;
    }

    /// <summary>
    /// Drops cooldown entries that can no longer suppress anything. Without this the dictionary
    /// grows once per session for the process lifetime; sessions are never revisited after their
    /// report is built, so the entries are pure leak. Only runs once the map is large enough to be
    /// worth walking.
    /// </summary>
    private static void PruneRegradeCooldowns(DateTime now)
    {
        if (RegradeCooldowns.Count < 512)
            return;

        foreach (var entry in RegradeCooldowns)
        {
            if ((now - entry.Value).TotalSeconds >= RegradeCooldownSeconds)
                RegradeCooldowns.TryRemove(entry.Key, out _);
        }
    }

    public async Task<IntegrityPolicyStatusResponse> GetIntegrityPolicyStatusAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new KeyNotFoundException("Session not found.");
        
        return new IntegrityPolicyStatusResponse
        {
            Accepted = session.IntegrityPolicyAccepted,
            AcceptedAt = session.IntegrityPolicyAcceptedAt
        };
    }

    public async Task<IntegrityPolicyStatusResponse> AcceptIntegrityPolicyAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _repo.GetByIdAsync(sessionId, ct);
        if (session is null || session.UserId != userId)
            throw new KeyNotFoundException("Session not found.");

        if (session.Status != "Initialized")
            throw new InvalidOperationException("Session cannot accept policy at this status.");

        bool updated = await _repo.AcceptIntegrityPolicyIfAsync(sessionId, ct);
        if (updated)
        {
            session.IntegrityPolicyAccepted = true;
            session.IntegrityPolicyAcceptedAt = VietnamTime.Now;
        }

        return new IntegrityPolicyStatusResponse
        {
            Accepted = session.IntegrityPolicyAccepted,
            AcceptedAt = session.IntegrityPolicyAcceptedAt
        };
    }

    public async Task<HintResponse> HintAsync(Guid userId, Guid sessionId, Guid sessionQuestionId, CancellationToken ct)
    {
        // Step 1: ownership — the question must belong to this session, and the session to this
        // user. GetQuestionByIdAsync includes .Session so both checks are a single query.
        var question = await _repo.GetQuestionByIdAsync(sessionQuestionId, ct);
        if (question is null || question.SessionId != sessionId || question.Session.UserId != userId)
            throw new KeyNotFoundException("Question not found.");

        // Step 2: text comes from the DB row, never from the client (see HintRequest comment) —
        // question.Content is already loaded above.

        // Step 3: claim the one-time hint quota BEFORE calling ai-service. Claiming first (not
        // after a successful Gemini call) closes the spam window a request-in-flight would
        // otherwise leave open — see Step 5 for the refund-on-failure counterpart.
        var claimed = await _repo.TryConsumeHintAsync(sessionQuestionId, sessionId, ct);
        if (!claimed)
            throw new SessionGuardException(409, "hint_already_used",
                "A hint has already been used for this question.");

        // Step 4/5: call ai-service; refund the quota if it fails so the user doesn't lose their
        // one hint to a transient ai-service error.
        try
        {
            var hint = await _aiServiceClient.GetHintAsync(question.Content, ct);
            return new HintResponse { Hint = hint };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "HintAsync: ai-service call failed after claiming the hint quota — releasing it. " +
                "sessionQuestionId={SessionQuestionId}", sessionQuestionId);
            await _repo.ReleaseHintAsync(sessionQuestionId, ct);
            throw;
        }
    }


}

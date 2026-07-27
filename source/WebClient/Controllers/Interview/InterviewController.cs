using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebClient.Helpers;
using WebClient.Models.Evaluation;
using WebClient.Models.Feedback;
using WebClient.Models.Interview;
using WebClient.Services.Evaluation;
using WebClient.Services.Feedback;
using WebClient.Services.Interview;
using WebClient.Services.Payment;
using WebClient.Services.Profile;
using WebClient.Services.Shared;

namespace WebClient.Controllers.Interview;

public class InterviewController(
    IInterviewService interviewService,
    IBiometricsService biometricsService,
    IVerificationService verificationService,
    IFeedbackService feedbackService,
    IPaymentService paymentService) : Controller
{
    private const int HistoryPageSize = 5;
    private const int RecentSessionsPageSize = 6;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await BuildDashboardAsync(cancellationToken));
    }

    // Populates the dashboard plus the user's current credit balance (shown next to each
    // duration option — see CreditPricing/Views/Interview/Index.cshtml). Balance stays 0 if the
    // user id can't be resolved from the auth cookie or the lookup fails; PaymentService already
    // swallows its own errors and returns an empty CreditBalanceDto (see GetCreditBalanceAsync).
    private async Task<DashboardViewModel> BuildDashboardAsync(CancellationToken cancellationToken)
    {
        var dashboard = await interviewService.GetDashboardAsync(cancellationToken);
        var userId = JwtUserId.FromToken(Request.Cookies["access_token"]);
        if (userId is not null)
        {
            var balance = await paymentService.GetCreditBalanceAsync(userId.Value, cancellationToken);
            dashboard.CreditBalance = balance.Balance;
        }
        return dashboard;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(DashboardViewModel model, CancellationToken cancellationToken)
    {
        // Biometric entry gate: a face enrollment is required before any session is created.
        // (InterviewAPI enforces this again server-side in InitAsync — this check just gives
        // the user a friendly redirect instead of a raw API error.)
        var (statusOk, biometricStatus, _) = await biometricsService.GetStatusAsync(cancellationToken);
        if (statusOk && biometricStatus is not null && !biometricStatus.FaceEnrolled)
        {
            TempData["InfoMessage"] =
                "Bạn cần xác thực khuôn mặt (enroll) trước khi bắt đầu phỏng vấn.";
            return RedirectToAction("Identity", "Profile");
        }

        var result = await interviewService.StartInterviewAsync(
            model.DurationMinutes, model.Level, model.Position, model.Jd,
            model.FocusAreas ?? new List<string>(), cancellationToken);

        if (!result.Success)
        {
            var dashboard = await BuildDashboardAsync(cancellationToken);
            dashboard.DurationMinutes = model.DurationMinutes;
            dashboard.Level = model.Level;
            dashboard.Position = model.Position;
            dashboard.Jd = model.Jd;
            dashboard.FocusAreas = model.FocusAreas ?? new List<string>();
            dashboard.ErrorMessage = result.ErrorMessage;
            return View("Index", dashboard);
        }

        // Entry gate: face verification happens on the Verify screen BEFORE the Room loads
        // (and before any credit is deducted — the deduction fires on the Room's first
        // question request, which InterviewAPI blocks until verification has passed).
        return RedirectToAction(nameof(Verify), new { id = result.SessionId });
    }

    // Biometric entry gate screen: liveness face check for this session before the Room.
    public async Task<IActionResult> Verify(Guid id, CancellationToken cancellationToken)
    {
        var (ok, gate, error) = await verificationService.GetEntryGateAsync(id, cancellationToken);
        if (!ok || gate is null)
        {
            TempData["ErrorMessage"] = error ?? "Could not check the verification status.";
            return RedirectToAction(nameof(Index));
        }

        if (gate.Allowed)
            return RedirectToAction(nameof(Room), new { id });

        if (!gate.FaceEnrolled)
        {
            TempData["InfoMessage"] =
                "Bạn cần xác thực khuôn mặt (enroll) trước khi bắt đầu phỏng vấn.";
            return RedirectToAction("Identity", "Profile");
        }

        if (gate.AttemptsRemaining <= 0)
        {
            TempData["ErrorMessage"] =
                "Xác thực khuôn mặt thất bại sau 3 lần. Phiên này đã bị huỷ — vui lòng tạo phiên phỏng vấn mới.";
            return RedirectToAction(nameof(Index));
        }

        return View(new VerifyViewModel { SessionId = id, AttemptsRemaining = gate.AttemptsRemaining });
    }

    // The Verify page's JS lands here after the final failed attempt: set the message
    // server-side (TempData) and send the user back to the dashboard.
    public IActionResult VerifyFailed()
    {
        TempData["ErrorMessage"] =
            "Xác thực khuôn mặt thất bại sau 3 lần. Phiên này đã bị huỷ — vui lòng tạo phiên phỏng vấn mới.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Catalog(CancellationToken cancellationToken)
    {
        var (success, catalog, error) = await interviewService.GetCatalogAsync(cancellationToken);
        if (!success || catalog is null)
        {
            return BadRequest(new { error = error ?? "Could not load the catalog." });
        }

        return Json(catalog);
    }

    // Statuses that may still render as an interactive Room. Anything else (Completed,
    // Cancelled, Expired, ...) is a session that has already ended.
    private static readonly string[] ActiveRoomStatuses = { "Initialized", "InProgress", "Paused" };

    public async Task<IActionResult> Room(Guid id, CancellationToken cancellationToken)
    {
        // Never cache this response and disable the back/forward cache for it. Without this, a
        // browser Back after the interview ends (redirected away by finishInterview()) can
        // restore this exact page from bfcache with NO request to the server at all — skipping
        // every guard below entirely. This is the primary fix for that: the status check right
        // after only covers requests that DO reach the server (a fresh Back navigation once
        // bfcache is disabled, a stale bookmark/link, etc).
        Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        Response.Headers.Append("Pragma", "no-cache");
        Response.Headers.Append("Expires", "0");

        // Status guard FIRST, before the entry gate: a session that has already ended must never
        // render as an interactive Room again, regardless of entry-gate state (a Completed
        // session's gate is still Allowed=true from its original verification, so the gate check
        // alone would let it through). Uses GetRoomAsync/Resume itself as the status source — it
        // already returns Status, and is also the exact call the happy path needs anyway.
        var (statusOk, room, statusError) = await interviewService.GetRoomAsync(id, cancellationToken);
        if (!statusOk || room is null)
        {
            TempData["ErrorMessage"] = statusError ?? "Could not load the session.";
            return RedirectToAction(nameof(Index));
        }

        if (!ActiveRoomStatuses.Contains(room.Status))
        {
            TempData["InfoMessage"] = "This interview has already ended.";
            return RedirectToAction("Report", "Evaluation", new { sessionId = id, durationActualSec = 0 });
        }

        // Entry gate guard: typing the Room URL directly cannot skip the Verify screen.
        // (InterviewAPI additionally blocks question serving server-side without a passed check.)
        // Fail-CLOSED: if the entry-gate check itself fails, do not open the Room — the
        // Room's first question request would only die with a confusing 400 anyway.
        var (gateOk, gate, _) = await verificationService.GetEntryGateAsync(id, cancellationToken);
        if (!gateOk || gate is null)
        {
            TempData["ErrorMessage"] = "Không thể xác minh danh tính, vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }

        if (!gate.Allowed)
        {
            if (gate.FaceEnrolled && gate.AttemptsRemaining > 0)
                return RedirectToAction(nameof(Verify), new { id });

            TempData["ErrorMessage"] = gate.Reason;
            return RedirectToAction(nameof(Index));
        }

        // Materialize the current/first question now that the entry gate has passed (this is
        // also where the first-entry credit deduction happens, server-side). Idempotent — safe
        // to call again on every Room entry/resume. Only for Initialized/InProgress — a Paused
        // session already has a current question and GenerateQuestionAsync rejects any status
        // other than those two, so calling it here for Paused would wrongly fail a simple
        // page refresh while paused.
        if (room.Status is "Initialized" or "InProgress")
        {
            var (generated, generateError) = await interviewService.GenerateQuestionAsync(id, cancellationToken);
            if (!generated)
            {
                TempData["ErrorMessage"] = generateError ?? "Could not load the interview question.";
                return RedirectToAction(nameof(Index));
            }

            // Re-fetch: generate may have materialized the first question / advanced the status,
            // neither of which the room snapshot from the status check above reflects.
            var (success, freshRoom, error) = await interviewService.GetRoomAsync(id, cancellationToken);
            if (!success || freshRoom is null)
            {
                TempData["ErrorMessage"] = error ?? "Could not load the session.";
                return RedirectToAction(nameof(Index));
            }

            room = freshRoom;
        }

        return View(room);
    }

    public async Task<IActionResult> History(
        int page = 1,
        string? status = null,
        string? position = null,
        string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var model = await interviewService.GetHistoryAsync(page, HistoryPageSize, status, position, keyword, cancellationToken);
        model.SuccessMessage = TempData["SuccessMessage"] as string;
        model.ErrorMessage ??= TempData["ErrorMessage"] as string;
        return View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await interviewService.GetSessionDetailAsync(id, cancellationToken);
        await LoadMyInterviewFeedbackAsync(model, cancellationToken);
        model.FeedbackMessage = TempData["FeedbackMessage"] as string;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> RecentSessions(int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await interviewService.GetRecentSessionsAsync(page, RecentSessionsPageSize, cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return StatusCode(result.StatusCode == 0 ? StatusCodes.Status500InternalServerError : result.StatusCode,
                new { error = result.Error ?? "Could not load recent sessions." });
        }

        return Json(new
        {
            page = result.Data.Page,
            totalPages = result.Data.TotalPages,
            totalItems = result.Data.TotalItems,
            items = result.Data.Items.Select(session => new
            {
                sessionId = session.SessionId,
                title = session.DisplayTitle,
                level = session.Level,
                status = session.Status,
                displayStatus = session.DisplayStatus,
                createdAt = session.CreatedAt,
                relativeTime = TimeFormat.RelativeTime(session.EndedAt ?? session.StartedAt ?? session.CreatedAt),
                detailsUrl = Url.Action(nameof(Details), "Interview", new { id = session.SessionId })
            })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSession(
        Guid id,
        int page = 1,
        string? status = null,
        string? position = null,
        string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var result = await interviewService.DeleteSessionAsync(id, cancellationToken);
        if (result.Success)
        {
            TempData["SuccessMessage"] = "The session was removed from your history.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Error ?? "Could not remove this session from history.";
        }

        return RedirectToAction(nameof(History), new { page, pageSize = HistoryPageSize, status, position, keyword });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitInterviewFeedback(
        Guid id,
        SubmitInterviewFeedbackRequest request,
        string? source,
        CancellationToken cancellationToken)
    {
        request.SessionId = id;
        request.Comment = FeedbackInput.NormalizeOptionalComment(request.Comment);
        var isEvaluationSource = string.Equals(source, "Evaluation", StringComparison.OrdinalIgnoreCase);

        if (!ModelState.IsValid)
        {
            var fieldMessage = CommentValidationMessage();
            var validationSummary = fieldMessage ?? "Please choose a 1-5 star rating for each category.";
            return await RedisplayInterviewFeedbackAsync(id, request, isEvaluationSource, validationSummary, fieldMessage, cancellationToken);
        }

        var result = await feedbackService.SubmitInterviewFeedbackAsync(request, cancellationToken);
        if (result.Success)
        {
            TempData["FeedbackMessage"] = "Interview feedback submitted and published successfully.";
            return isEvaluationSource
                ? RedirectToAction("Report", "Evaluation", new { sessionId = id })
                : RedirectToAction(nameof(Details), new { id });
        }

        if (result.StatusCode == StatusCodes.Status401Unauthorized)
        {
            var returnUrl = isEvaluationSource
                ? Url.Action("Report", "Evaluation", new { sessionId = id })
                : Url.Action(nameof(Details), "Interview", new { id });
            return RedirectToAction("Login", "Auth", new { returnUrl });
        }

        if (result.StatusCode == StatusCodes.Status409Conflict)
        {
            var existing = await feedbackService.GetMyInterviewFeedbackAsync(id, cancellationToken);
            if (existing.Success && existing.Data is not null)
            {
                TempData["FeedbackMessage"] = "Your existing feedback is shown below.";
                if (isEvaluationSource)
                {
                    return RedirectToAction("Report", "Evaluation", new { sessionId = id });
                }

                var duplicateModel = await interviewService.GetSessionDetailAsync(id, cancellationToken);
                duplicateModel.MyInterviewFeedback = existing.Data;
                duplicateModel.FeedbackMessage = "Your existing feedback is shown below.";
                return View(nameof(Details), duplicateModel);
            }

            return await RedisplayInterviewFeedbackAsync(
                id,
                request,
                isEvaluationSource,
                "Feedback already exists for this interview session, but it could not be reloaded right now.",
                null,
                cancellationToken);
        }

        var message = ToFeedbackMessage(result.StatusCode, result.Error, "Could not submit interview feedback.");
        var validationMessage = result.StatusCode is StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity
            && result.Error?.Contains("Comment", StringComparison.OrdinalIgnoreCase) == true
                ? result.Error
                : null;
        return await RedisplayInterviewFeedbackAsync(id, request, isEvaluationSource, message, validationMessage, cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> Message(Guid id, [FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        var (success, message, error) = await interviewService.SendMessageAsync(id, request, cancellationToken);

        if (!success || message is null)
        {
            return BadRequest(new { error = error ?? "Could not send the answer." });
        }

        return Json(message);
    }

    [HttpPost]
    public async Task<IActionResult> Tts([FromBody] TtsProxyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Text))
        {
            return BadRequest(new { error = "Text is required." });
        }

        var (success, audio, error) = await interviewService.GetTtsAudioAsync(request.Text, cancellationToken);

        if (!success || audio is null)
        {
            return BadRequest(new { error = error ?? "Could not synthesize speech." });
        }

        return File(audio, "audio/mpeg");
    }

    [HttpPost]
    public async Task<IActionResult> Stt(IFormFile audio, CancellationToken cancellationToken)
    {
        if (audio is null || audio.Length == 0)
        {
            return BadRequest(new { error = "Audio is required." });
        }

        using var stream = new MemoryStream();
        await audio.CopyToAsync(stream, cancellationToken);
        var contentType = string.IsNullOrEmpty(audio.ContentType) ? "audio/webm" : audio.ContentType;
        var fileName = string.IsNullOrEmpty(audio.FileName) ? "answer.webm" : audio.FileName;

        var (success, text, error) = await interviewService.TranscribeAudioAsync(
            stream.ToArray(), fileName, contentType, cancellationToken);

        if (!success)
        {
            return BadRequest(new { error = error ?? "Could not transcribe audio." });
        }

        return Json(new { text });
    }

    [HttpPost]
    public async Task<IActionResult> Hint([FromBody] HintProxyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Question))
        {
            return BadRequest(new { error = "Question is required." });
        }

        var (success, hint, error) = await interviewService.GetHintAsync(request.Question, cancellationToken);

        if (!success)
        {
            return BadRequest(new { error = error ?? "Could not get a hint." });
        }

        return Json(new { hint });
    }

    [HttpPost]
    public async Task<IActionResult> CompleteSession(Guid id, CancellationToken cancellationToken)
    {
        var (success, completion, error) = await interviewService.CompleteSessionAsync(id, cancellationToken);

        if (!success || completion is null)
        {
            return BadRequest(new { error = error ?? "Could not complete the session." });
        }

        return Json(new { status = completion.Status });
    }

    [HttpPost]
    public async Task<IActionResult> EndSession(Guid id, CancellationToken cancellationToken)
    {
        var (success, cancellation, error) = await interviewService.EndSessionAsync(id, cancellationToken);

        if (!success || cancellation is null)
        {
            return BadRequest(new { error = error ?? "Could not cancel the session." });
        }

        return Json(new { status = cancellation.Status });
    }

    [HttpPost]
    public async Task<IActionResult> Pause(Guid id, CancellationToken cancellationToken)
    {
        var (success, pause, error) = await interviewService.PauseSessionAsync(id, cancellationToken);

        if (!success || pause is null)
        {
            return BadRequest(new { error = error ?? "Could not pause the session." });
        }

        return Json(pause);
    }

    // Same-origin proxy for flagging a mandatory-camera policy violation (permission denied /
    // disconnected mid-session). Flag-only — never blocks the Room or answering.
    [HttpPost]
    public async Task<IActionResult> CameraViolation(
        Guid id, [FromBody] CameraViolationProxyRequest request, CancellationToken cancellationToken)
    {
        var (success, error) = await interviewService.ReportCameraViolationAsync(id, request.Reason, cancellationToken);

        if (!success)
        {
            return BadRequest(new { error = error ?? "Could not report the camera violation." });
        }

        return Json(new { ok = true });
    }

    // Same-origin proxy for un-pausing the interview (Room UX Phase 2).
    [HttpPost]
    public async Task<IActionResult> Unpause(Guid id, CancellationToken cancellationToken)
    {
        var (success, unpause, error) = await interviewService.UnpauseSessionAsync(id, cancellationToken);

        if (!success || unpause is null)
        {
            return BadRequest(new { error = error ?? "Could not resume the session." });
        }

        return Json(unpause);
    }

    [HttpPost]
    public async Task<IActionResult> TourSeen(CancellationToken cancellationToken)
    {
        var (success, error) = await interviewService.MarkRoomTourSeenAsync(cancellationToken);

        if (!success)
        {
            return BadRequest(new { error = error ?? "Could not save tour status." });
        }

        return Json(new { ok = true });
    }


    private async Task<IActionResult> RedisplayInterviewFeedbackAsync(
        Guid sessionId,
        SubmitInterviewFeedbackRequest request,
        bool isEvaluationSource,
        string message,
        string? validationMessage,
        CancellationToken cancellationToken)
    {
        if (isEvaluationSource)
        {
            TempData[FeedbackTempDataKeys.InterviewForm] = JsonSerializer.Serialize(request);
            TempData[FeedbackTempDataKeys.InterviewValidation] = validationMessage;
            TempData["FeedbackMessage"] = message;
            return RedirectToAction("Report", "Evaluation", new { sessionId });
        }

        var model = await interviewService.GetSessionDetailAsync(sessionId, cancellationToken);
        model.FeedbackForm = request;
        model.FeedbackFormWasSubmitted = true;
        model.FeedbackValidationMessage = validationMessage;
        model.FeedbackMessage = message;
        await LoadMyInterviewFeedbackAsync(model, cancellationToken);
        return View(nameof(Details), model);
    }

    private string? CommentValidationMessage()
    {
        if (!ModelState.TryGetValue(nameof(SubmitInterviewFeedbackRequest.Comment), out var entry))
        {
            return null;
        }

        return entry.Errors.Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));
    }

    private async Task LoadMyInterviewFeedbackAsync(SessionDetailViewModel model, CancellationToken cancellationToken)
    {
        var result = await feedbackService.GetMyInterviewFeedbackAsync(model.SessionId, cancellationToken);
        if (result.Success)
        {
            model.MyInterviewFeedback = result.Data;
        }
        else if (result.StatusCode != StatusCodes.Status404NotFound &&
                 result.StatusCode != StatusCodes.Status401Unauthorized &&
                 result.StatusCode != StatusCodes.Status403Forbidden)
        {
            model.FeedbackMessage ??= result.Error;
        }
    }

    private static string ToFeedbackMessage(int statusCode, string? error, string fallback) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity => error ?? "Please correct the feedback fields and try again.",
            StatusCodes.Status403Forbidden => "You do not have permission to submit feedback for this session.",
            StatusCodes.Status404NotFound => "This session, report, or feedback is no longer available.",
            StatusCodes.Status429TooManyRequests => "Too many requests. Please wait and try again.",
            StatusCodes.Status500InternalServerError or StatusCodes.Status502BadGateway or StatusCodes.Status503ServiceUnavailable or StatusCodes.Status504GatewayTimeout => "Feedback is temporarily unavailable. Please try again later.",
            _ => string.IsNullOrWhiteSpace(error) ? fallback : error
        };
}

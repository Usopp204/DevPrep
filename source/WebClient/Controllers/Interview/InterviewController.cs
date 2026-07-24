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

namespace WebClient.Controllers.Interview;

public class InterviewController(
    IInterviewService interviewService,
    IFeedbackService feedbackService) : Controller
{
    private const int HistoryPageSize = 5;
    private const int RecentSessionsPageSize = 6;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await interviewService.GetDashboardAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(DashboardViewModel model, CancellationToken cancellationToken)
    {
        var result = await interviewService.StartInterviewAsync(
            model.DurationMinutes, model.Level, model.Position, model.Jd,
            model.FocusAreas ?? new List<string>(), cancellationToken);

        if (!result.Success)
        {
            var dashboard = await interviewService.GetDashboardAsync(cancellationToken);
            dashboard.DurationMinutes = model.DurationMinutes;
            dashboard.Level = model.Level;
            dashboard.Position = model.Position;
            dashboard.Jd = model.Jd;
            dashboard.FocusAreas = model.FocusAreas ?? new List<string>();
            dashboard.ErrorMessage = result.ErrorMessage;
            return View("Index", dashboard);
        }

        return RedirectToAction(nameof(Room), new { id = result.SessionId });
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

    public async Task<IActionResult> Room(Guid id, CancellationToken cancellationToken)
    {
        var (success, room, error) = await interviewService.GetRoomAsync(id, cancellationToken);

        if (!success || room is null)
        {
            TempData["ErrorMessage"] = error ?? "Could not load the session.";
            return RedirectToAction(nameof(Index));
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

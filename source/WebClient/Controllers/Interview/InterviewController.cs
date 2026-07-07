using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Interview;
using WebClient.Services.Interview;

namespace WebClient.Controllers.Interview;

public class InterviewController(IInterviewService interviewService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await interviewService.GetDashboardAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Start(DashboardViewModel model, CancellationToken cancellationToken)
    {
        var result = await interviewService.StartInterviewAsync(
            model.DurationMinutes, model.Level, model.Position, model.Jd,
            model.FocusAreas ?? new List<string>(), cancellationToken);

        if (!result.Success)
        {
            model.ErrorMessage = result.ErrorMessage;
            return View("Index", model);
        }

        return RedirectToAction(nameof(Room), new { id = result.SessionId });
    }

    // Same-origin proxy: returns the ai-service roles/technologies catalog for the dashboard JS.
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

    public async Task<IActionResult> History(Guid? sessionId, CancellationToken cancellationToken)
    {
        var model = await interviewService.GetHistoryAsync(sessionId, cancellationToken);
        return View(model);
    }

    // Same-origin proxy for the Room's chat: the browser sends the HttpOnly JWT cookie here,
    // and the server forwards the answer to the Gateway (Authorization attached server-side).
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

    // Same-origin proxy for text-to-speech: returns audio/mpeg bytes the Room plays when
    // "AI Voice" is on. Binary passthrough — not JSON.
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

    // Same-origin proxy for speech-to-text: forwards the recorded audio to the Gateway and returns
    // the transcribed text, which the Room drops into the answer box for the user to review + send.
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

    // Same-origin proxy for a hint on the current question.
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

    // Same-origin proxy for ending the interview (see Message above).
    [HttpPost]
    public async Task<IActionResult> EndSession(Guid id, CancellationToken cancellationToken)
    {
        var (success, error) = await interviewService.EndSessionAsync(id, cancellationToken);

        if (!success)
        {
            return BadRequest(new { error = error ?? "Could not end the session." });
        }

        return Json(new { ok = true });
    }

    // Same-origin proxy for pausing the interview (Room UX Phase 2).
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

    // Same-origin proxy for marking the Room's onboarding tour as seen (Room UX Phase 3) — called
    // once the tour finishes or is skipped/closed. Not session-scoped (the flag is per-user).
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
}

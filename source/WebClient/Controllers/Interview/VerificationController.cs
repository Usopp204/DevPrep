using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Interview;

namespace WebClient.Controllers.Interview;

// Same-origin proxies for the biometric verification flow (mirrors Interview/Stt):
// the browser posts here with the HttpOnly JWT cookie, the server attaches the
// Bearer header and forwards to the Gateway.
public class VerificationController(IVerificationService verificationService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> InSessionCheck(
        Guid sessionId, Guid sessionQuestionId, CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty || sessionQuestionId == Guid.Empty)
            return BadRequest(new { error = "Session and question are required." });

        var (success, check, error) = await verificationService.GetInSessionCheckAsync(
            sessionId, sessionQuestionId, cancellationToken);
        if (!success || check is null)
            return BadRequest(new { error = error ?? "Could not poll the verification schedule." });
        return Json(check);
    }

    [HttpGet]
    public async Task<IActionResult> Challenge(Guid sessionId, string modality, CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty || string.IsNullOrWhiteSpace(modality))
        {
            return BadRequest(new { error = "sessionId and modality are required." });
        }

        var (success, challenge, error) = await verificationService.GetChallengeAsync(
            sessionId, modality, cancellationToken);
        if (!success || challenge is null)
        {
            return BadRequest(new { error = error ?? "Could not get a challenge." });
        }

        return Json(challenge);
    }

    [HttpPost]
    public async Task<IActionResult> VerifyFace(Guid sessionId, List<IFormFile> frames, CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty || frames is null || frames.Count == 0)
        {
            return BadRequest(new { error = "At least one frame is required." });
        }

        var buffered = new List<(byte[] Data, string FileName, string ContentType)>();
        foreach (var frame in frames)
        {
            if (frame.Length == 0) continue;
            using var stream = new MemoryStream();
            await frame.CopyToAsync(stream, cancellationToken);
            buffered.Add((stream.ToArray(),
                string.IsNullOrEmpty(frame.FileName) ? "frame.jpg" : frame.FileName,
                string.IsNullOrEmpty(frame.ContentType) ? "image/jpeg" : frame.ContentType));
        }

        var (success, result, error) = await verificationService.VerifyFaceAsync(
            sessionId, buffered, cancellationToken);
        if (!success || result is null)
        {
            return BadRequest(new { error = error ?? "Face verification failed." });
        }

        return Json(result);
    }

    // Entry-gate status for the Verify screen (attempts remaining, allowed) — same-origin proxy.
    [HttpGet]
    public async Task<IActionResult> EntryGate(Guid sessionId, CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty)
        {
            return BadRequest(new { error = "sessionId is required." });
        }

        var (success, status, error) = await verificationService.GetEntryGateAsync(sessionId, cancellationToken);
        if (!success || status is null)
        {
            return BadRequest(new { error = error ?? "Could not check the entry gate." });
        }

        return Json(status);
    }

    // One in-session cheating check (single frame, no challenge/liveness) — the Room posts here
    // once per new question; InterviewAPI rate-limits how often a check actually runs.
    [HttpPost]
    public async Task<IActionResult> VerifyInSession(
        Guid sessionId,
        [FromForm] Guid checkId,
        [FromForm] IFormFile frame,
        CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty || checkId == Guid.Empty || frame is null || frame.Length == 0)
        {
            return BadRequest(new { error = "A frame is required." });
        }

        using var stream = new MemoryStream();
        await frame.CopyToAsync(stream, cancellationToken);
        var fileName = string.IsNullOrEmpty(frame.FileName) ? "frame.jpg" : frame.FileName;
        var contentType = string.IsNullOrEmpty(frame.ContentType) ? "image/jpeg" : frame.ContentType;

        var (success, result, error) = await verificationService.VerifyInSessionAsync(
            sessionId, checkId, stream.ToArray(), fileName, contentType, cancellationToken);
        if (!success || result is null)
        {
            return BadRequest(new { error = error ?? "In-session check failed." });
        }

        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> VerifyVoice(Guid sessionId, IFormFile audio, CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty || audio is null || audio.Length == 0)
        {
            return BadRequest(new { error = "An audio recording is required." });
        }

        using var stream = new MemoryStream();
        await audio.CopyToAsync(stream, cancellationToken);
        var contentType = string.IsNullOrEmpty(audio.ContentType) ? "audio/webm" : audio.ContentType;
        var fileName = string.IsNullOrEmpty(audio.FileName) ? "voice.webm" : audio.FileName;

        var (success, result, error) = await verificationService.VerifyVoiceAsync(
            sessionId, stream.ToArray(), fileName, contentType, cancellationToken);
        if (!success || result is null)
        {
            return BadRequest(new { error = error ?? "Voice verification failed." });
        }

        return Json(result);
    }
}

using System.Security.Claims;
using InterviewAPI.Clients;
using InterviewAPI.DTOs;
using InterviewAPI.DTOs.Biometrics;
using InterviewAPI.DTOs.Session;
using InterviewAPI.Services;
using InterviewAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewAPI.Controllers;

[ApiController]
[Route("api/interview/sessions/{sessionId:guid}/verify")]
[Authorize]
public class SessionVerificationController : ControllerBase
{
    private readonly IBiometricVerificationService _service;
    private readonly ISessionService _sessionService;
    private readonly ILogger<SessionVerificationController> _logger;

    // NOTE for future passes: the `catch (BiometricQualityException)` and
    // `catch (KeyNotFoundException)` blocks in this file deliberately return ex.Message and are NOT
    // leaks. BiometricQualityException relays ai-service's actionable reason ("No face detected"),
    // and KeyNotFoundException is always the authored string "Session not found.". Only the generic
    // `catch (Exception)` blocks were leaking raw dependency text; those are the ones fixed here.
    public SessionVerificationController(
        IBiometricVerificationService service,
        ISessionService sessionService,
        ILogger<SessionVerificationController> logger)
    {
        _service = service;
        _sessionService = sessionService;
        _logger = logger;
    }

    [HttpGet("in-session-check")]
    public async Task<IActionResult> GetInSessionCheck(
        Guid sessionId, [FromQuery] Guid sessionQuestionId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _sessionService.GetInSessionCheckAsync(
                userId, sessionId, sessionQuestionId, cancellationToken);
            return Ok(ApiResponse<InSessionCheckDueResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "In-session check lookup failed for session {SessionId}.", sessionId);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail("Could not check verification status. Please try again shortly."));
        }
    }

    // GET api/interview/sessions/{sessionId}/verify/challenge?modality=face|voice
    // Issues a fresh server-side challenge and returns only the instruction to perform.
    [HttpGet("challenge")]
    public async Task<IActionResult> GetChallenge(
        Guid sessionId, [FromQuery] string modality, CancellationToken cancellationToken)
    {
        var normalized = NormalizeModality(modality);
        if (normalized is null)
        {
            return BadRequest(ApiResponse<object>.Fail("modality must be 'face' or 'voice'."));
        }

        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.IssueChallengeAsync(userId, sessionId, normalized, cancellationToken);
            return Ok(ApiResponse<VerificationChallengeResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Issuing a {Modality} challenge failed for session {SessionId}.", normalized, sessionId);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail("Could not start the identity check. Please try again shortly."));
        }
    }

    // POST api/interview/sessions/{sessionId}/verify/face?phase=Entry|InSession — burst of
    // frames captured while the user performs the prompted head turn. phase is recorded on
    // the audit event (Milestone 4 groundwork); defaults to Entry for existing callers.
    [HttpPost("face")]
    public async Task<IActionResult> VerifyFace(
        Guid sessionId, List<IFormFile> frames, CancellationToken cancellationToken,
        [FromQuery] string phase = "Entry")
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.VerifyFaceAsync(userId, sessionId, frames, cancellationToken, phase);
            return Ok(ApiResponse<VerificationResultResponse>.Ok(result));
        }
        // KEEP ex.Message — ai-service's quality reason is what tells the user how to retake the shot.
        catch (BiometricQualityException ex)
        {
            return UnprocessableEntity(ApiResponse<object>.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Face verification failed for session {SessionId}, phase {Phase}.", sessionId, phase);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail("Face verification is temporarily unavailable. Please try again shortly."));
        }
    }

    // POST api/interview/sessions/{sessionId}/verify/voice?phase=Entry|InSession — recording
    // of the user reading the challenge phrase aloud.
    [HttpPost("voice")]
    public async Task<IActionResult> VerifyVoice(
        Guid sessionId, IFormFile file, CancellationToken cancellationToken,
        [FromQuery] string phase = "Entry")
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.VerifyVoiceAsync(userId, sessionId, file, cancellationToken, phase);
            return Ok(ApiResponse<VerificationResultResponse>.Ok(result));
        }
        // KEEP ex.Message — see VerifyFace above.
        catch (BiometricQualityException ex)
        {
            return UnprocessableEntity(ApiResponse<object>.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Voice verification failed for session {SessionId}, phase {Phase}.", sessionId, phase);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail("Voice verification is temporarily unavailable. Please try again shortly."));
        }
    }

    // GET api/interview/sessions/{sessionId}/verify/entry-gate — Milestone 4 groundwork.
    // Read-only decision the Room UI would check before letting the candidate in; not
    // called from any Room UI yet (Room integration is a separate, later step).
    [HttpGet("entry-gate")]
    public async Task<IActionResult> GetEntryGateStatus(Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.GetEntryGateStatusAsync(userId, sessionId, cancellationToken);
            return Ok(ApiResponse<EntryGateStatusResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Entry-gate status lookup failed for session {SessionId}.", sessionId);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail("Could not check verification status. Please try again shortly."));
        }
    }

    // POST api/interview/sessions/{sessionId}/verify/in-session — one background cheating check
    // (single frame, no challenge/liveness), called by the Room once per new question. There is
    // no time-based rate limit: the throttle is that GetInSessionCheckAsync issues at most one
    // pending checkId per SessionQuestion and TryConsumeQuestionCheckAsync makes it one-time-use,
    // with the checkId expiring after Biometrics:InSessionCheckTtlSeconds (appsettings.json: 20;
    // the code default is the same 20 seconds). Flag-only by default
    // (Biometrics:InSessionStrictness); only ends the session when Strictness="Strict" and the
    // strike cap is reached.
    [HttpPost("in-session")]
    public async Task<IActionResult> VerifyInSession(
        Guid sessionId,
        [FromForm] Guid checkId,
        [FromForm] IFormFile frame,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.VerifyInSessionAsync(userId, sessionId, checkId, frame, cancellationToken);
            return Ok(ApiResponse<InSessionVerifyResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "In-session verification failed for session {SessionId}, check {CheckId}.", sessionId, checkId);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail("Identity check is temporarily unavailable. Please try again shortly."));
        }
    }

    // POST api/interview/sessions/{sessionId}/verify/voice-answer — transcribes one microphone
    // answer and performs a speaker-only identity check. A confirmed mismatch is one direct
    // cheating strike; quality failures are not strikes.
    [HttpPost("voice-answer")]
    public async Task<IActionResult> VerifyVoiceAnswer(
        Guid sessionId,
        [FromForm] Guid checkId,
        [FromForm] IFormFile audio,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.VerifyVoiceAnswerAsync(
                userId, sessionId, checkId, audio, cancellationToken);
            return Ok(ApiResponse<VoiceAnswerVerifyResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Voice-answer verification failed for session {SessionId}, check {CheckId}.", sessionId, checkId);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail("Voice verification is temporarily unavailable. Please try again shortly."));
        }
    }

    // POST api/interview/sessions/{sessionId}/verify/tab-event — the Room reports that the
    // candidate left the interview tab / window, or pasted into the answer box. Phase 1 is
    // RECORD ONLY: the audit row carries Modality="Tab", which the strike math ignores entirely,
    // so this endpoint can never warn the candidate or end a session.
    //
    // Unlike the other actions here it does not use the try/catch mapping — the service reports
    // its expected outcomes as data (TabEventResponse.Outcome) precisely so each one can get its
    // own status code:
    //   204 monitoring disabled (Biometrics:Enabled or Biometrics:TabSwitchEnabled off)
    //   403 the session belongs to someone else (or does not exist)
    //   409 the session is not InProgress
    //   400 unknown EventType, or a missing IncidentId
    //   200 recorded, ignored as too short (Recorded=false), or already reported (Duplicated=true)
    [HttpPost("tab-event")]
    public async Task<IActionResult> RecordTabEvent(
        Guid sessionId, [FromBody] RecordTabEventRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(ApiResponse<object>.Fail("A tab event payload is required."));

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.RecordTabEventAsync(userId, sessionId, request, cancellationToken);

        return result.Outcome switch
        {
            BiometricVerificationService.TabOutcomeDisabled => NoContent(),
            BiometricVerificationService.TabOutcomeNotOwner =>
                StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(result.Message)),
            BiometricVerificationService.TabOutcomeNotInProgress =>
                Conflict(ApiResponse<object>.Fail(result.Message)),
            BiometricVerificationService.TabOutcomeInvalidEventType or
            BiometricVerificationService.TabOutcomeInvalidIncidentId =>
                BadRequest(ApiResponse<object>.Fail(result.Message)),
            _ => Ok(ApiResponse<TabEventResponse>.Ok(result, result.Message)),
        };
    }

    private static string? NormalizeModality(string? modality) =>
        modality?.Trim().ToLowerInvariant() switch
        {
            "face" => ProfileBiometricsService.ModalityFace,
            "voice" => ProfileBiometricsService.ModalityVoice,
            _ => null,
        };
}

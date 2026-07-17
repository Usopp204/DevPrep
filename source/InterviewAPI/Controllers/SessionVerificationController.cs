using System.Security.Claims;
using InterviewAPI.Clients;
using InterviewAPI.DTOs;
using InterviewAPI.DTOs.Biometrics;
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

    public SessionVerificationController(IBiometricVerificationService service)
    {
        _service = service;
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
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
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
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
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
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
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
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // GET api/interview/sessions/{sessionId}/verify/in-session-check-due — Milestone 4
    // groundwork. Randomized-cadence signal; not polled by any Room UI yet.
    [HttpGet("in-session-check-due")]
    public async Task<IActionResult> GetInSessionCheckDue(Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.IsInSessionCheckDueAsync(sessionId, cancellationToken);
            return Ok(ApiResponse<InSessionCheckDueResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private static string? NormalizeModality(string? modality) =>
        modality?.Trim().ToLowerInvariant() switch
        {
            "face" => ProfileBiometricsService.ModalityFace,
            "voice" => ProfileBiometricsService.ModalityVoice,
            _ => null,
        };
}

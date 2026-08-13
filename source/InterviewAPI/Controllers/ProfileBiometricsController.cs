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
[Route("api/profiles/me/biometrics")]
[Authorize]
public class ProfileBiometricsController : ControllerBase
{
    private readonly IProfileBiometricsService _service;
    private readonly ILogger<ProfileBiometricsController> _logger;

    public ProfileBiometricsController(
        IProfileBiometricsService service,
        ILogger<ProfileBiometricsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET api/profiles/me/biometrics — per-modality enrollment status for the profile page.
    [HttpGet]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.GetStatusAsync(userId, cancellationToken);
            return Ok(ApiResponse<BiometricStatusResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loading biometric enrollment status failed.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail("Could not load your verification status. Please try again shortly."));
        }
    }

    // POST api/profiles/me/biometrics/face — enroll (or re-enroll) the face template.
    [HttpPost("face")]
    public Task<IActionResult> EnrollFace(IFormFile file, CancellationToken cancellationToken) =>
        EnrollAsync(ProfileBiometricsService.ModalityFace, file, cancellationToken);

    // POST api/profiles/me/biometrics/voice — enroll (or re-enroll) the voice template.
    [HttpPost("voice")]
    public Task<IActionResult> EnrollVoice(IFormFile file, CancellationToken cancellationToken) =>
        EnrollAsync(ProfileBiometricsService.ModalityVoice, file, cancellationToken);

    private async Task<IActionResult> EnrollAsync(string modality, IFormFile file, CancellationToken ct)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _service.EnrollAsync(userId, modality, file, ct);
            return Ok(ApiResponse<BiometricEnrollResponse>.Ok(result, "Enrolled."));
        }
        // KEEP ex.Message on both typed catches below — they are NOT ex.Message leaks.
        // ai-service produces genuinely actionable text here ("No face detected", "Audio too
        // short"), and WebClient renders it verbatim (BiometricsService.cs:53-54 comments on
        // exactly this). Replacing these with a generic string would delete the only information
        // telling the user how to retake their sample. Do not "finish the job" on them.
        catch (BiometricQualityException ex)
        {
            // Quality rejection from ai-service — the message is user-facing.
            return UnprocessableEntity(ApiResponse<object>.Fail(ex.Message));
        }
        catch (BiometricIdentityMismatchException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Biometric enrollment failed for modality {Modality}.", modality);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail("Enrollment is temporarily unavailable. Please try again shortly."));
        }
    }
}

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

    public ProfileBiometricsController(IProfileBiometricsService service)
    {
        _service = service;
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
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
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
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}

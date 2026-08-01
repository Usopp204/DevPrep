using System.Security.Claims;
using FeedbackAPI.Clients;
using FeedbackAPI.DTOs;
using FeedbackAPI.Services;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeedbackAPI.Controllers;

[ApiController]
[Route("api/feedback")]
public class FeedbackController : ControllerBase
{
    private readonly ISystemFeedbackService _systemService;
    private readonly IAiFeedbackService _aiService;
    private readonly IFeedbackReportService _reportService;

    public FeedbackController(
        ISystemFeedbackService systemService,
        IAiFeedbackService aiService,
        IFeedbackReportService reportService)
    {
        _systemService = systemService;
        _aiService = aiService;
        _reportService = reportService;
    }

    [HttpGet("system/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSystemReviews([FromQuery] PublicFeedbackQuery query, CancellationToken cancellationToken)
    {
        var result = await _systemService.GetPublicReviewsAsync(query, cancellationToken);
        await _reportService.ApplySystemViewerStateAsync(
            result.Items,
            GetCandidateViewerId(),
            User.IsInRole("User"),
            cancellationToken);

        return Ok(ApiResponse<PagedResult<PublicSystemFeedbackDto>>.Ok(result));
    }

    [HttpGet("ai/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAiReviews([FromQuery] PublicFeedbackQuery query, CancellationToken cancellationToken)
    {
        var result = await _aiService.GetPublicReviewsAsync(
            query,
            GetCandidateViewerId(),
            cancellationToken);
        return Ok(ApiResponse<PagedResult<PublicAiFeedbackDto>>.Ok(result));
    }

    [HttpGet("system/submission-status")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetSystemSubmissionStatus(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));

        var result = await _systemService.GetSubmissionStatusAsync(userId, cancellationToken);
        return Ok(ApiResponse<SystemFeedbackSubmissionStatusDto>.Ok(result));
    }

    [HttpGet("system/{feedbackId:guid}/my-feedback")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetMySystemFeedback(Guid feedbackId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));

        var result = await _systemService.GetMyFeedbackAsync(feedbackId, userId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<object>.Fail("System feedback not found."))
            : Ok(ApiResponse<MySystemFeedbackDto>.Ok(result));
    }

    [HttpPost("system")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> SubmitSystem([FromBody] SubmitSystemFeedbackRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));

        try
        {
            var result = await _systemService.SubmitAsync(userId, request, Request.Headers.Authorization.ToString(), cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<CreatedSystemFeedbackDto>.Ok(result));
        }
        catch (FeedbackValidationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (FeedbackEligibilityException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }

        catch (InterviewApiUnauthorizedException)
        {
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));
        }
        catch (InterviewApiForbiddenException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("Forbidden."));
        }
        catch (InterviewApiTimeoutException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("InterviewAPI request timed out."));
        }
        catch (InterviewApiDependencyException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("InterviewAPI dependency failure."));
        }
    }


    [HttpGet("interview/sessions/{sessionId:guid}/my-feedback")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetMyInterviewFeedback(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));

        try
        {
            var result = await _aiService.GetMyFeedbackAsync(userId, sessionId, cancellationToken);
            if (result is null)
                return NotFound(ApiResponse<object>.Fail("No interview feedback found for this session."));

            return Ok(ApiResponse<MyInterviewFeedbackDto>.Ok(result));
        }
        catch (FeedbackValidationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
    [HttpPost("interview")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> SubmitInterview([FromBody] SubmitInterviewFeedbackRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));

        try
        {
            var result = await _aiService.SubmitAsync(userId, request, Request.Headers.Authorization.ToString(), cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<CreatedInterviewFeedbackDto>.Ok(result));
        }
        catch (FeedbackValidationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (DuplicateFeedbackException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (FeedbackEligibilityException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InterviewApiUnauthorizedException)
        {
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));
        }
        catch (InterviewApiForbiddenException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("Forbidden."));
        }
        catch (InterviewApiNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InterviewApiTimeoutException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("InterviewAPI request timed out."));
        }
        catch (InterviewApiDependencyException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("InterviewAPI dependency failure."));
        }
    }

    [HttpPut("system/{feedbackId:guid}")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> EditSystem(Guid feedbackId, [FromBody] UpdateSystemFeedbackRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));
        try
        {
            await _systemService.EditAsync(feedbackId, userId, request, cancellationToken);
            return Ok(ApiResponse<object?>.Ok(null, "System feedback updated successfully."));
        }
        catch (FeedbackValidationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
        catch (FeedbackNotFoundException) { return NotFound(ApiResponse<object>.Fail("System feedback not found.")); }
    }

    [HttpDelete("system/{feedbackId:guid}")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> DeleteSystem(Guid feedbackId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));
        try
        {
            await _systemService.CandidateDeleteAsync(feedbackId, userId, cancellationToken);
            return Ok(ApiResponse<object?>.Ok(null, "System feedback deleted successfully."));
        }
        catch (FeedbackNotFoundException) { return NotFound(ApiResponse<object>.Fail("System feedback not found.")); }
    }

    [HttpPut("interview/{feedbackId:guid}")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> EditInterview(Guid feedbackId, [FromBody] UpdateInterviewFeedbackRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));
        try
        {
            await _aiService.EditAsync(feedbackId, userId, request, cancellationToken);
            return Ok(ApiResponse<object?>.Ok(null, "Interview feedback updated successfully."));
        }
        catch (FeedbackValidationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
        catch (FeedbackNotFoundException) { return NotFound(ApiResponse<object>.Fail("Interview feedback not found.")); }
    }

    [HttpDelete("interview/{feedbackId:guid}")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> DeleteInterview(Guid feedbackId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));
        try
        {
            await _aiService.CandidateDeleteAsync(feedbackId, userId, cancellationToken);
            return Ok(ApiResponse<object?>.Ok(null, "Interview feedback deleted successfully."));
        }
        catch (FeedbackNotFoundException) { return NotFound(ApiResponse<object>.Fail("Interview feedback not found.")); }
    }

    [HttpPost("system/reviews/{feedbackId:guid}/reports")]
    [Consumes("application/json")]
    [Authorize(Roles = "User")]
    public Task<IActionResult> ReportSystemFeedback(
        Guid feedbackId,
        [FromBody] CreateFeedbackReportRequest request,
        CancellationToken cancellationToken) =>
        CreateReportAsync(FeedbackAPI.Validation.FeedbackReportType.System, feedbackId, request, cancellationToken);

    private async Task<IActionResult> CreateReportAsync(
        string feedbackType,
        Guid feedbackId,
        CreateFeedbackReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));
        }


        try
        {
            var result = await _reportService.CreateAsync(
                feedbackType,
                feedbackId,
                userId,
                request,
                cancellationToken);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<CreatedFeedbackReportDto>.Ok(result, result.Message));
        }
        catch (FeedbackReportValidationException exception)
        {
            return BadRequest(ApiResponse<object>.Fail(exception.Message));
        }
        catch (FeedbackReportForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(exception.Message));
        }
        catch (FeedbackReportNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Fail(exception.Message));
        }
        catch (DuplicateFeedbackReportException exception)
        {
            return Conflict(ApiResponse<object>.Fail(exception.Message));
        }
    }

    private Guid? GetCandidateViewerId() =>
        User.IsInRole("User") && TryGetUserId(out var userId) ? userId : null;

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }
}

using System.Security.Claims;
using EvaluationAPI.Clients;
using EvaluationAPI.DTOs;
using EvaluationAPI.DTOs.Evaluation;
using EvaluationAPI.Services;
using EvaluationAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EvaluationAPI.Controllers;

[ApiController]
[Route("api/evaluation/sessions")]
[Authorize]
public class EvaluationController : ControllerBase
{
    private readonly IEvaluationService _service;
    private readonly IInterviewApiClient _interviewApiClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EvaluationController(
        IEvaluationService service,
        IInterviewApiClient interviewApiClient,
        IHttpContextAccessor httpContextAccessor)
    {
        _service = service;
        _interviewApiClient = interviewApiClient;
        _httpContextAccessor = httpContextAccessor;
    }

    private async Task<bool> UserOwnsSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var token = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        return await _interviewApiClient.OwnsSessionAsync(sessionId, token, ct);
    }

    private IActionResult Forbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("You do not have access to this session."));

    private IActionResult EvaluationUnavailable() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("Evaluation is temporarily unavailable."));

    [HttpPost("{id:guid}/answers/{answerId:guid}")]
    public async Task<IActionResult> EvaluateAnswer(
        Guid id,
        Guid answerId,
        [FromBody] EvaluateAnswerRequest req,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await UserOwnsSessionAsync(id, cancellationToken))
                return Forbidden();

            var result = await _service.EvaluateAnswerAsync(id, answerId, req, cancellationToken);
            return Ok(ApiResponse<EvaluateAnswerResponse>.Ok(result));
        }
        catch (InterviewApiUnauthorizedException)
        {
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));
        }
        catch (InterviewApiForbiddenException)
        {
            return Forbidden();
        }
        catch (InterviewApiSessionNotFoundException)
        {
            return Forbidden();
        }
        catch (InterviewApiTimeoutException)
        {
            return EvaluationUnavailable();
        }
        catch (InterviewApiDependencyException)
        {
            return EvaluationUnavailable();
        }
        catch (EvaluationDependencyException)
        {
            return EvaluationUnavailable();
        }
        catch (EvaluationPersistenceException)
        {
            return EvaluationUnavailable();
        }
        catch (EvaluationRequestException)
        {
            return BadRequest(ApiResponse<object>.Fail("The grading request is invalid."));
        }
        catch (ArgumentException)
        {
            return BadRequest(ApiResponse<object>.Fail("The grading request is invalid."));
        }
        catch (InvalidOperationException)
        {
            return BadRequest(ApiResponse<object>.Fail("The grading request is invalid."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return EvaluationUnavailable();
        }
    }

    [HttpGet("{sessionId:guid}/performance")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetPerformance(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out _))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));

        try
        {
            var bearerToken = Request.Headers.Authorization.ToString();
            var result = await _service.GetPerformanceReportAsync(sessionId, bearerToken, cancellationToken);
            return Ok(ApiResponse<PerformanceReportResponse>.Ok(result));
        }
        catch (InterviewApiUnauthorizedException)
        {
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));
        }
        catch (InterviewApiForbiddenException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("You do not have access to this session."));
        }
        catch (InterviewApiSessionNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (SessionNotEligibleForPerformanceReportException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InterviewApiTimeoutException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("Evaluation data is unavailable."));
        }
        catch (InterviewApiDependencyException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("Evaluation data is unavailable."));
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("Evaluation data is unavailable."));
        }
    }

    /// <summary>Lấy danh sách kết quả chấm điểm từng câu trả lời của một phiên.</summary>
    [HttpGet("{id:guid}/answers")]
    public async Task<IActionResult> GetAnswerEvaluations(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            if (!await UserOwnsSessionAsync(id, cancellationToken))
                return Forbidden();

            var result = await _service.GetAnswerEvaluationsAsync(
                id,
                Request.Headers.Authorization.ToString(),
                cancellationToken);
            return Ok(ApiResponse<List<EvaluateAnswerResponse>>.Ok(result));
        }
        catch (InterviewApiUnauthorizedException)
        {
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));
        }
        catch (InterviewApiForbiddenException)
        {
            return Forbidden();
        }
        catch (InterviewApiSessionNotFoundException)
        {
            return Forbidden();
        }
        catch (InterviewApiTimeoutException)
        {
            return EvaluationUnavailable();
        }
        catch (InterviewApiDependencyException)
        {
            return EvaluationUnavailable();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return BadRequest(ApiResponse<object>.Fail("Unable to load answer evaluations."));
        }
    }

    private bool TryGetAuthenticatedUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out userId);
    }
}
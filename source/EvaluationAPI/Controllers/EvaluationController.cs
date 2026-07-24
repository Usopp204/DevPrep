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

    /// <summary>Chấm điểm một câu trả lời của phiên (gọi ai-service), lưu vào AnswerEvaluations.</summary>
    [HttpPost("{id:guid}/answers/{answerId:guid}")]
    public async Task<IActionResult> EvaluateAnswer(
        Guid id, Guid answerId, [FromBody] EvaluateAnswerRequest req, CancellationToken cancellationToken)
    {
        try
        {
            if (!await UserOwnsSessionAsync(id, cancellationToken))
                return Forbidden();

            var result = await _service.EvaluateAnswerAsync(id, answerId, req, cancellationToken);
            return Ok(ApiResponse<EvaluateAnswerResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Tổng hợp báo cáo cuối từ các đánh giá đã chấm (gọi ai-service), lưu FeedbackReports + PerformanceMetrics.</summary>
    [HttpPost("{id:guid}/report")]
    public async Task<IActionResult> BuildReport(
        Guid id, [FromBody] BuildReportRequest req, CancellationToken cancellationToken)
    {
        try
        {
            if (!await UserOwnsSessionAsync(id, cancellationToken))
                return Forbidden();

            var result = await _service.BuildReportAsync(
                id,
                req,
                Request.Headers.Authorization.ToString(),
                cancellationToken);
            return Ok(ApiResponse<ReportResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Lấy báo cáo đã tồn tại của một phiên (dùng để xem lại, không gọi ai-service).</summary>
    [HttpGet("{id:guid}/report")]
    public async Task<IActionResult> GetReport(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            if (!await UserOwnsSessionAsync(id, cancellationToken))
                return Forbidden();

            var result = await _service.GetExistingReportAsync(
                id,
                Request.Headers.Authorization.ToString(),
                cancellationToken);
            if (result is null)
                return NotFound(ApiResponse<object>.Fail("No report found for this session."));
            return Ok(ApiResponse<ReportResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("{sessionId:guid}/performance")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetPerformance(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));

        try
        {
            var bearerToken = Request.Headers.Authorization.ToString();
            var result = await _service.GetPerformanceReportAsync(userId, sessionId, bearerToken, cancellationToken);
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
        catch (ReportNotFoundException ex)
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

    /// <summary>Lấy danh sách kết quả chấm điểm từng câu trả lời của một phiên.</summary>
    [HttpGet("{id:guid}/answers")]
    public async Task<IActionResult> GetAnswerEvaluations(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            if (!await UserOwnsSessionAsync(id, cancellationToken))
                return Forbidden();

            var result = await _service.GetAnswerEvaluationsAsync(id, cancellationToken);
            return Ok(ApiResponse<List<EvaluateAnswerResponse>>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private bool TryGetAuthenticatedUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out userId);
    }
}

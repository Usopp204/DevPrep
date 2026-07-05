using EvaluationAPI.Clients;
using EvaluationAPI.DTOs;
using EvaluationAPI.DTOs.Evaluation;
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

    // Guards every action against IDOR: the Evaluation tables have no UserId, so ownership is
    // verified against InterviewAPI (SessionId -> InterviewSessions.UserId), forwarding the JWT.
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

            var result = await _service.BuildReportAsync(id, req, cancellationToken);
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

            var result = await _service.GetExistingReportAsync(id, cancellationToken);
            if (result is null)
                return NotFound(ApiResponse<object>.Fail("No report found for this session."));
            return Ok(ApiResponse<ReportResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
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
}

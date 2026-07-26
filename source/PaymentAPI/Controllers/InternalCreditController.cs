using Microsoft.AspNetCore.Mvc;
using PaymentAPI.DTOs;
using PaymentAPI.Services;

namespace PaymentAPI.Controllers;

[ApiController]
[Route("api/internal/credit")]
public class InternalCreditController : ControllerBase
{
    private readonly InternalCreditService _service;

    public InternalCreditController(InternalCreditService service)
    {
        _service = service;
    }

    [HttpPost("deduct")]
    [ProducesResponseType(typeof(InternalCreditDeductionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeductCredit(
        [FromBody] InternalCreditDeductionRequest request,
        CancellationToken ct)
    {
        var response = await _service.DeductCreditAsync(request, ct);
        return Ok(response);
    }

    [HttpPost("refund")]
    [ProducesResponseType(typeof(InternalCreditDeductionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefundCredit(
        [FromBody] InternalCreditRefundRequest request,
        CancellationToken ct)
    {
        var response = await _service.RefundCreditAsync(request, ct);
        return Ok(response);
    }

    // One-time bonus (100 credits for enrolling both Face and Voice biometrics). Idempotent —
    // see InternalCreditService.AwardCreditAsync for the two-layer guard.
    [HttpPost("award")]
    [ProducesResponseType(typeof(InternalCreditAwardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AwardCredit(
        [FromBody] InternalCreditAwardRequest request,
        CancellationToken ct)
    {
        var response = await _service.AwardCreditAsync(request, ct);
        return Ok(response);
    }
}
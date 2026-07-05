using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentAPI.DTOs;
using PaymentAPI.Services;

namespace PaymentAPI.Controllers;

// Reached through the API Gateway's "payment-route" (/api/payment/{**catch-all}),
// which strips the "/api/payment" prefix before forwarding — so this route must
// match the already-stripped path.
[ApiController]
[Route("transactions")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _service;

    public TransactionsController(ITransactionService service)
    {
        _service = service;
    }

    /// <summary>
    /// View my transaction history (credit purchase orders) + wallet balance.
    /// GET /api/payment/transactions
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<TransactionHistoryResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(ApiResponse<TransactionHistoryResultDto>.Fail("Invalid or missing token."));

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var result = await _service.GetHistoryAsync(userId.Value, page, pageSize, ct);
        return Ok(ApiResponse<TransactionHistoryResultDto>.Ok(result));
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}

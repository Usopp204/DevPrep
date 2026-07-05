using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.DTOs;

namespace PaymentAPI.Controllers;

[ApiController]
[Route("api/admin/manager/payments")]
[Authorize(Roles = "Manager")]
public class ManagerPaymentsController : ControllerBase
{
    private readonly PaymentDbContext _db;

    public ManagerPaymentsController(PaymentDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ManagerPaymentListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList([FromQuery] ManagerPaymentQuery query, CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var orders = _db.Orders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            orders = orders.Where(o => o.OrderCode.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
            orders = orders.Where(o => o.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.PaymentMethod))
            orders = orders.Where(o => o.PaymentMethod == query.PaymentMethod);

        if (query.UserId.HasValue)
            orders = orders.Where(o => o.UserId == query.UserId.Value);

        if (query.DateFrom.HasValue)
            orders = orders.Where(o => o.CreatedAt >= query.DateFrom.Value);

        if (query.DateTo.HasValue)
            orders = orders.Where(o => o.CreatedAt <= query.DateTo.Value);

        if (query.MinAmount.HasValue)
            orders = orders.Where(o => o.Amount >= query.MinAmount.Value);

        if (query.MaxAmount.HasValue)
            orders = orders.Where(o => o.Amount <= query.MaxAmount.Value);

        var totalCount = await orders.CountAsync(cancellationToken);
        var totalAmount = await orders.SumAsync(o => (decimal?)o.Amount, cancellationToken) ?? 0m;
        var paidAmount = await orders
            .Where(o => o.Status == "Paid")
            .SumAsync(o => (decimal?)o.Amount, cancellationToken) ?? 0m;
        var paidCount = await orders.CountAsync(o => o.Status == "Paid", cancellationToken);

        var items = await (
            from order in orders
            join tx in _db.PaymentTransactions.AsNoTracking()
                on order.OrderId equals tx.OrderId into txGroup
            from tx in txGroup.OrderByDescending(t => t.CreatedAt).Take(1).DefaultIfEmpty()
            orderby order.CreatedAt descending, order.OrderId descending
            select new ManagerPaymentListItemDto
            {
                OrderId = order.OrderId,
                OrderCode = order.OrderCode,
                UserId = order.UserId,
                PackageId = order.PackageId,
                Amount = order.Amount,
                Credits = order.Credits,
                Currency = order.Currency,
                PaymentMethod = order.PaymentMethod,
                Status = order.Status,
                PaidAt = order.PaidAt,
                CreatedAt = order.CreatedAt,
                VnpTransactionNo = tx == null ? null : tx.VnpTransactionNo,
                VnpBankCode = tx == null ? null : tx.VnpBankCode,
                VnpResponseCode = tx == null ? null : tx.VnpResponseCode,
                TransactionStatus = tx == null ? null : tx.Status
            })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var result = new ManagerPaymentListResultDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalAmount = totalAmount,
            PaidAmount = paidAmount,
            PaidCount = paidCount
        };

        return Ok(ApiResponse<ManagerPaymentListResultDto>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ManagerPaymentDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == id, cancellationToken);

        if (order is null)
            return NotFound(ApiResponse<ManagerPaymentDetailDto>.Fail($"Payment with id '{id}' not found."));

        var tx = await _db.PaymentTransactions.AsNoTracking()
            .Where(t => t.OrderId == id)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var detail = new ManagerPaymentDetailDto
        {
            OrderId = order.OrderId,
            OrderCode = order.OrderCode,
            UserId = order.UserId,
            PackageId = order.PackageId,
            Amount = order.Amount,
            Credits = order.Credits,
            Currency = order.Currency,
            PaymentMethod = order.PaymentMethod,
            Status = order.Status,
            ExpiredAt = order.ExpiredAt,
            PaidAt = order.PaidAt,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            VnpTransactionNo = tx?.VnpTransactionNo,
            VnpBankCode = tx?.VnpBankCode,
            VnpResponseCode = tx?.VnpResponseCode,
            TransactionStatus = tx?.Status,
            Transaction = tx is null ? null : new ManagerPaymentTransactionDto
            {
                TransactionId = tx.TransactionId,
                OrderId = tx.OrderId,
                Amount = tx.Amount,
                VnpTransactionNo = tx.VnpTransactionNo,
                VnpBankCode = tx.VnpBankCode,
                VnpResponseCode = tx.VnpResponseCode,
                VnpPayDate = tx.VnpPayDate,
                SecureHashValid = tx.SecureHashValid,
                Status = tx.Status,
                RawCallbackJson = tx.RawCallbackJson,
                CreatedAt = tx.CreatedAt
            }
        };

        return Ok(ApiResponse<ManagerPaymentDetailDto>.Ok(detail));
    }
}

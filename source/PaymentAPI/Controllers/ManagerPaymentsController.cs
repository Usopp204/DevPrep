using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;

namespace PaymentAPI.Controllers;

[ApiController]
[Route("api/admin/manager/payments")]
[Authorize(Roles = "Manager")]
public class ManagerPaymentsController : ControllerBase
{
    private readonly PaymentDbContext _db;
    private readonly ILogger<ManagerPaymentsController> _logger;
    private readonly string _connString;

    public ManagerPaymentsController(PaymentDbContext db, ILogger<ManagerPaymentsController> logger, IConfiguration config)
    {
        _db = db;
        _logger = logger;
        _connString = config.GetValue<string>("PAYMENT_DB_CONNECTION_STRING")
            ?? db.Database.GetDbConnection().ConnectionString;
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
            orders = orders.Where(o => o.CreatedAt < query.DateTo.Value.Date.AddDays(1));

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

        var pageOrders = await orders
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.OrderId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var orderIds = pageOrders.Select(o => o.OrderId).ToList();

        var transactions = orderIds.Count > 0
            ? await _db.PaymentTransactions.AsNoTracking()
                .Where(t => orderIds.Contains(t.OrderId))
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken)
            : new List<PaymentTransaction>();

        var txDict = transactions
            .GroupBy(t => t.OrderId)
            .ToDictionary(g => g.Key, g => g.First());

        var items = pageOrders.Select(order =>
        {
            txDict.TryGetValue(order.OrderId, out var tx);
            return new ManagerPaymentListItemDto
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
                VnpTransactionNo = tx?.VnpTransactionNo,
                VnpBankCode = tx?.VnpBankCode,
                VnpResponseCode = tx?.VnpResponseCode,
                TransactionStatus = tx?.Status
            };
        }).ToList();

        await EnrichWithUserNamesAsync(items, cancellationToken);

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

        await EnrichWithUserNamesAsync([detail], cancellationToken);

        return Ok(ApiResponse<ManagerPaymentDetailDto>.Ok(detail));
    }

    private async Task EnrichWithUserNamesAsync(List<ManagerPaymentListItemDto> items, CancellationToken ct)
    {
        if (items.Count == 0) return;
        try
        {
            // New orders store AccountId. The second profile join keeps old seed/backup rows
            // that stored UserProfile.UserId readable without changing the API contract.
            var storedUserIds = items.Select(i => i.UserId).Distinct().ToList();
            var connString = _connString;
            var nameMap = new Dictionary<Guid, (string? FullName, string? Email)>();

            using var conn = new SqlConnection(connString);
            await conn.OpenAsync(ct);
            var valueRows = string.Join(",", storedUserIds.Select((_, index) => $"(@UserId{index})"));
            var sql = $@"
                SELECT 
                    ids.StoredUserId,
                    COALESCE(profileByAccount.FullName, profileByUserId.FullName),
                    account.Email
                FROM (VALUES {valueRows}) ids(StoredUserId)
                LEFT JOIN DevPrep_User.dbo.UserProfiles profileByAccount
                    ON profileByAccount.AccountId = ids.StoredUserId
                LEFT JOIN DevPrep_User.dbo.UserProfiles profileByUserId
                    ON profileByUserId.UserId = ids.StoredUserId
                LEFT JOIN DevPrep_Auth.dbo.Accounts account
                    ON account.AccountId = COALESCE(
                        profileByAccount.AccountId,
                        profileByUserId.AccountId,
                        ids.StoredUserId
                    )";

            _logger.LogInformation("EnrichWithUserNames: querying {Count} stored user ids.", storedUserIds.Count);

            using var cmd = new SqlCommand(sql, conn);
            for (var index = 0; index < storedUserIds.Count; index++)
                cmd.Parameters.AddWithValue($"@UserId{index}", storedUserIds[index]);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var storedUserId = reader.GetGuid(0);
                var name = reader.IsDBNull(1) ? null : reader.GetString(1);
                var email = reader.IsDBNull(2) ? null : reader.GetString(2);
                nameMap[storedUserId] = (name, email);
            }

            _logger.LogInformation("EnrichWithUserNames: found {Count} user records", nameMap.Count);

            foreach (var item in items)
            {
                if (nameMap.TryGetValue(item.UserId, out var info))
                {
                    item.FullName = !string.IsNullOrWhiteSpace(info.FullName) ? info.FullName : info.Email;
                    item.Email = info.Email;
                }
                else
                {
                    _logger.LogWarning("EnrichWithUserNames: no user found for stored UserId = {UserId}", item.UserId);
                }
            }
        }
        catch (Exception ex)
        {
            // Log lỗi để debug, không crash endpoint
            _logger.LogError(ex, "EnrichWithUserNamesAsync failed");
        }
    }
}

using PaymentAPI.Repositories;

namespace PaymentAPI.Endpoints;

/// <summary>
/// The buyer's own purchase history.
/// </summary>
/// <remarks>
/// Moved out of Program.cs — see the note on <see cref="CreditPackageEndpoints"/>.
///
/// Reads through IOrderRepository rather than the Admin payment routes, which is the whole
/// difference between this and /admin/payments: the repository call is scoped to one user id,
/// so there is no filter a caller could drop to see somebody else's orders.
/// </remarks>
public static class TransactionEndpoints
{
    public static void MapTransactionEndpoints(this WebApplication app)
    {
        app.MapGet("/transactions", async (HttpContext ctx, IOrderRepository orderRepo,
                                           int page = 1, int pageSize = 10) =>
        {
            // Same rule as /vnpay/order: the identity comes from the gateway header, never the query.
            var userIdStr = ctx.Request.Headers["X-User-Id"].FirstOrDefault();
            if (!Guid.TryParse(userIdStr, out var userId))
                return Results.BadRequest("Missing or invalid X-User-Id header.");

            // Clamped so a caller cannot ask for one enormous page and turn this into a table scan.
            pageSize = Math.Clamp(pageSize, 1, 50);
            page     = Math.Max(1, page);

            var result = await orderRepo.GetPagedByUserAsync(userId, page, pageSize);

            // Projected rather than returned whole: Order carries UserId and PackageId, which the
            // history screen does not display and does not need to receive.
            var items = result.Items.Select(o => new
            {
                o.OrderId, o.OrderCode, o.Amount, o.Credits,
                o.Currency, o.PaymentMethod, o.Status, o.PaidAt, o.CreatedAt
            });
            return Results.Ok(new { TotalCount = result.TotalCount, Page = page, PageSize = pageSize, Items = items });
        });
    }
}

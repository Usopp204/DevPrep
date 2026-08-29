using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.Models;

namespace PaymentAPI.Endpoints;

public static class CreditLedgerEndpoints
{
    public static void MapCreditLedgerEndpoints(this WebApplication app) =>
        app.MapGet("/credits/ledger", HandleAsync);

    public static async Task<IResult> HandleAsync(HttpContext ctx, PaymentDbContext db,
        string? type = null, int page = 1, int pageSize = 10)
    {
        var userIdText = ctx.Request.Headers["X-User-Id"].FirstOrDefault();
        if (!Guid.TryParse(userIdText, out var userId))
            return Results.BadRequest("Missing or invalid X-User-Id header.");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.Set<CreditLedgerEntry>().AsNoTracking().Where(x => x.UserId == userId);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.EntryType == type);
        var totalCount = await query.CountAsync();
        var all = db.Set<CreditLedgerEntry>().AsNoTracking().Where(x => x.UserId == userId);
        var totalUsed = await all.Where(x => x.EntryType == "Usage").SumAsync(x => (int?)-x.CreditChange) ?? 0;
        var totalRefunded = await all.Where(x => x.EntryType == "Refund").SumAsync(x => (int?)x.CreditChange) ?? 0;
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.LedgerId, x.EntryType, x.CreditChange, x.BalanceAfter, x.ReferenceId, x.Description, x.CreatedAt }).ToListAsync();
        return Results.Ok(new { TotalCount = totalCount, Page = page, PageSize = pageSize, TotalUsed = totalUsed, TotalRefunded = totalRefunded, Items = items });
    }
}

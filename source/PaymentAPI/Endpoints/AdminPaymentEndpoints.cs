using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.Repositories;

namespace PaymentAPI.Endpoints;

/// <summary>
/// Admin payment monitoring: the order list with its filters and aggregates, the revenue
/// report, and one order's detail.
/// </summary>
/// <remarks>
/// Moved out of Program.cs - see the note on <see cref="CreditPackageEndpoints"/>.
///
/// These read through a raw SqlConnection rather than EF. The list combines paging with
/// aggregates over the whole filtered set (total amount, paid amount, paid count), and the
/// revenue report groups by day; expressing both in LINQ meant either several round trips
/// or a query EF could not translate. The filter arguments are still passed as parameters,
/// never concatenated into the SQL.
/// </remarks>
public static class AdminPaymentEndpoints
{
    public static void MapAdminPaymentEndpoints(this WebApplication app)
    {
        // --- Admin: GET /admin/payments (filter + search + aggregates) ---
        app.MapGet("/admin/payments", async (
            PaymentDbContext db,
            int page = 1, int pageSize = 20,
            string? keyword = null, string? status = null, string? paymentMethod = null,
            DateTime? dateFrom = null, DateTime? dateTo = null,
            decimal? minAmount = null, decimal? maxAmount = null) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page     = Math.Max(1, page);

            var orders = db.Orders.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                orders = orders.Where(o => o.OrderCode.Contains(kw));
            }
            if (!string.IsNullOrWhiteSpace(status))
                orders = orders.Where(o => o.Status == status);
            if (!string.IsNullOrWhiteSpace(paymentMethod))
                orders = orders.Where(o => o.PaymentMethod == paymentMethod);
            if (dateFrom.HasValue)
                orders = orders.Where(o => o.CreatedAt >= dateFrom.Value);
            if (dateTo.HasValue)
                orders = orders.Where(o => o.CreatedAt < dateTo.Value.AddDays(1));
            if (minAmount.HasValue)
                orders = orders.Where(o => o.Amount >= minAmount.Value);
            if (maxAmount.HasValue)
                orders = orders.Where(o => o.Amount <= maxAmount.Value);

            var totalCount  = await orders.CountAsync();
            var totalAmount = await orders.SumAsync(o => (decimal?)o.Amount) ?? 0m;
            var paidAmount  = await orders.Where(o => o.Status == "Paid").SumAsync(o => (decimal?)o.Amount) ?? 0m;
            var paidCount   = await orders.CountAsync(o => o.Status == "Paid");

            var items = await orders
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new
                {
                    o.OrderId, o.OrderCode, o.UserId, o.Amount, o.Credits,
                    o.Currency, o.PaymentMethod, o.Status, o.PaidAt, o.CreatedAt
                })
                .ToListAsync();

            var userNames = new Dictionary<Guid, string?>();
            if (items.Count > 0)
            {
                try
                {
                    var ids = items.Select(i => i.UserId).Distinct().ToList();
                    var inClause = string.Join(",", ids.Select((_, i) => $"@u{i}"));
                    var sql = $@"
                        SELECT a.AccountId, COALESCE(NULLIF(p.FullName, ''), a.Email) AS DisplayName
                        FROM DevPrep_Auth.dbo.Accounts a
                        LEFT JOIN DevPrep_User.dbo.UserProfiles p ON p.AccountId = a.AccountId
                        WHERE a.AccountId IN ({inClause});";
                    await using var conn = new SqlConnection(db.Database.GetConnectionString());
                    await conn.OpenAsync();
                    await using var cmd = new SqlCommand(sql, conn);
                    for (var i = 0; i < ids.Count; i++)
                        cmd.Parameters.AddWithValue($"@u{i}", ids[i]);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        userNames[reader.GetGuid(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);
                }
                catch { }
            }

            return Results.Ok(new
            {
                TotalCount = totalCount, Page = page, PageSize = pageSize,
                TotalAmount = totalAmount, PaidAmount = paidAmount, PaidCount = paidCount,
                Items = items.Select(o => new
                {
                    o.OrderId, o.OrderCode, o.UserId,
                    UserName = userNames.GetValueOrDefault(o.UserId),
                    o.Amount, o.Credits, o.Currency, o.PaymentMethod, o.Status, o.PaidAt, o.CreatedAt
                })
            });
        }).RequireAuthorization("AdminArea");

        // --- Admin: GET /admin/payments/revenue-report ---
        app.MapGet("/admin/payments/revenue-report", async (
            PaymentDbContext db,
            string period = "today", DateTime? from = null, DateTime? to = null) =>
        {
            var now = DateTime.UtcNow.AddHours(7); // giờ Việt Nam, khớp quy ước chung của codebase
            DateTime rangeFrom, rangeTo;

            switch (period.ToLowerInvariant())
            {
                case "month":
                    rangeFrom = new DateTime(now.Year, now.Month, 1);
                    rangeTo   = rangeFrom.AddMonths(1);
                    break;
                case "year":
                    rangeFrom = new DateTime(now.Year, 1, 1);
                    rangeTo   = rangeFrom.AddYears(1);
                    break;
                case "custom":
                    rangeFrom = (from ?? now.Date).Date;
                    rangeTo   = (to ?? now.Date).Date.AddDays(1);
                    break;
                default:
                    period    = "today";
                    rangeFrom = now.Date;
                    rangeTo   = rangeFrom.AddDays(1);
                    break;
            }

            var paidOrders = db.Orders.AsNoTracking()
                .Where(o => o.Status == "Paid" && o.PaidAt >= rangeFrom && o.PaidAt < rangeTo);

            var packagesSold = await paidOrders.CountAsync();
            var totalRevenue = await paidOrders.SumAsync(o => (decimal?)o.Amount) ?? 0m;
            var totalCredits = await paidOrders.SumAsync(o => (int?)o.Credits) ?? 0;

            return Results.Ok(new
            {
                Period = period,
                From = rangeFrom,
                To = rangeTo,
                PackagesSold = packagesSold,
                TotalRevenue = totalRevenue,
                TotalCredits = totalCredits
            });
        }).RequireAuthorization("AdminArea");

        // --- Admin: GET /admin/payments/revenue-daily ---
        // Vietnam-naive PaidAt calendar days, inclusive [from..to]. Returns an explicit row for every day.
        app.MapGet("/admin/payments/revenue-daily", async (
            PaymentDbContext db,
            DateOnly? from = null,
            DateOnly? to = null) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var rangeFrom = from ?? new DateOnly(today.Year, today.Month, 1);
            var rangeToInclusive = to ?? today;

            if (rangeFrom > rangeToInclusive)
                return Results.BadRequest(new { message = "The From date must be on or before the To date." });

            var inclusiveDays = rangeToInclusive.DayNumber - rangeFrom.DayNumber + 1;
            if (inclusiveDays > 366)
                return Results.BadRequest(new { message = "The selected date range must contain no more than 366 days." });

            var rangeStart = rangeFrom.ToDateTime(TimeOnly.MinValue);
            var rangeToExclusive = rangeToInclusive.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var paidVnpayOrders = db.Orders.AsNoTracking()
                .Where(order =>
                    order.Status == "Paid" &&
                    order.PaymentMethod.ToUpper() == "VNPAY" &&
                    order.Currency.ToUpper() == "VND" &&
                    order.PaidAt.HasValue);

            var totalRevenueAsOf = await paidVnpayOrders
                .Where(order => order.PaidAt!.Value < rangeToExclusive)
                .SumAsync(order => (decimal?)order.Amount) ?? 0m;
            var totalOrdersAsOf = await paidVnpayOrders
                .LongCountAsync(order => order.PaidAt!.Value < rangeToExclusive);
            var grouped = await paidVnpayOrders
                .Where(order =>
                    order.PaidAt!.Value >= rangeStart &&
                    order.PaidAt.Value < rangeToExclusive)
                .GroupBy(order => order.PaidAt!.Value.Date)
                .Select(group => new
                {
                    Date = group.Key,
                    Revenue = group.Sum(order => order.Amount),
                    Orders = group.LongCount()
                })
                .ToListAsync();

            var byDate = grouped.ToDictionary(
                item => DateOnly.FromDateTime(item.Date),
                item => (item.Revenue, item.Orders));
            var days = new List<DashboardRevenueDay>(inclusiveDays);
            for (var date = rangeFrom; date <= rangeToInclusive; date = date.AddDays(1))
            {
                var value = byDate.GetValueOrDefault(date);
                days.Add(new DashboardRevenueDay(date, value.Revenue, value.Orders));
            }

            return Results.Ok(new
            {
                From = rangeFrom,
                To = rangeToInclusive,
                TotalRevenueAsOf = totalRevenueAsOf,
                TotalOrdersAsOf = totalOrdersAsOf,
                PeriodRevenue = days.Sum(day => day.Revenue),
                PeriodOrders = days.Sum(day => day.Orders),
                Days = days
            });
        }).RequireAuthorization("AdminArea");

        // --- Admin: GET /admin/payments/{id} ---
        app.MapGet("/admin/payments/{id:guid}", async (Guid id, IOrderRepository orderRepo, ITransactionRepository txRepo) =>
        {
            var order = await orderRepo.GetByIdAsync(id);
            if (order is null) return Results.NotFound();
            var tx = await txRepo.GetByOrderIdAsync(id);
            return Results.Ok(new { Order = order, Transaction = tx });
        }).RequireAuthorization("AdminArea");
    }
}

record DashboardRevenueDay(DateOnly Date, decimal Revenue, long Orders);

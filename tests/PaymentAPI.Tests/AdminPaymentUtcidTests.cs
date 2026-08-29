using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentAPI.Controllers;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using Xunit;

namespace PaymentAPI.Tests;

public class AdminPaymentUtcidTests
{
    [Fact(DisplayName = "FUNC138-ADM-UTCID01")]
    public async Task List_returns_orders_totals_and_latest_transaction()
    {
        await using var db = Db();
        var paid = Order("INV-PAID", "Paid", 120, DateTime.UtcNow);
        var pending = Order("INV-PENDING", "Pending", 80, DateTime.UtcNow.AddMinutes(-1));
        db.Orders.AddRange(paid, pending);
        db.PaymentTransactions.AddRange(Transaction(paid, "old", paid.CreatedAt), Transaction(paid, "latest", paid.CreatedAt.AddMinutes(1)));
        await db.SaveChangesAsync();

        var data = Data(await Controller(db).GetList(new(), default));

        Assert.Equal(2, data.TotalCount);
        Assert.Equal(200, data.TotalAmount);
        Assert.Equal(120, data.PaidAmount);
        Assert.Equal(1, data.PaidCount);
        Assert.Equal("latest", data.Items.Single(x => x.OrderId == paid.OrderId).VnpTransactionNo);
    }

    [Fact(DisplayName = "FUNC138-ADM-UTCID02")]
    public async Task List_trims_and_applies_keyword()
    {
        await using var db = Db();
        db.Orders.AddRange(Order("MATCH-001", "Paid", 10, DateTime.UtcNow), Order("OTHER", "Paid", 20, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var data = Data(await Controller(db).GetList(new() { Keyword = "  MATCH  " }, default));

        Assert.Single(data.Items);
        Assert.Equal("MATCH-001", data.Items[0].OrderCode);
    }

    [Fact(DisplayName = "FUNC138-ADM-UTCID03")]
    public async Task List_applies_status_method_and_user_filters()
    {
        await using var db = Db();
        var user = Guid.NewGuid();
        var match = Order("MATCH", "Paid", 10, DateTime.UtcNow, user, "VNPay");
        db.Orders.AddRange(match, Order("WRONG-STATUS", "Failed", 10, DateTime.UtcNow, user, "VNPay"), Order("WRONG-METHOD", "Paid", 10, DateTime.UtcNow, user, "Cash"));
        await db.SaveChangesAsync();

        var data = Data(await Controller(db).GetList(new() { Status = "Paid", PaymentMethod = "VNPay", UserId = user }, default));

        Assert.Collection(data.Items, item => Assert.Equal(match.OrderId, item.OrderId));
    }

    [Fact(DisplayName = "FUNC138-ADM-UTCID04")]
    public async Task List_normalizes_invalid_page_and_clamps_page_size()
    {
        await using var db = Db();

        var data = Data(await Controller(db).GetList(new() { Page = -3, PageSize = 500 }, default));

        Assert.Equal(1, data.Page);
        Assert.Equal(100, data.PageSize);
        Assert.Empty(data.Items);
    }

    [Fact(DisplayName = "FUNC138-ADM-UTCID05")]
    public async Task List_includes_date_and_amount_boundaries()
    {
        await using var db = Db();
        var day = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);
        db.Orders.AddRange(Order("MIN", "Paid", 10, day), Order("MAX", "Paid", 20, day.AddDays(1).AddTicks(-1)), Order("OUT", "Paid", 21, day));
        await db.SaveChangesAsync();

        var data = Data(await Controller(db).GetList(new() { DateFrom = day, DateTo = day, MinAmount = 10, MaxAmount = 20 }, default));

        Assert.Equal(2, data.TotalCount);
        Assert.Contains(data.Items, x => x.OrderCode == "MIN");
        Assert.Contains(data.Items, x => x.OrderCode == "MAX");
    }

    [Fact(DisplayName = "FUNC138-ADM-UTCID06")]
    public async Task List_empty_database_returns_zero_summary()
    {
        await using var db = Db();

        var data = Data(await Controller(db).GetList(new(), default));

        Assert.Empty(data.Items);
        Assert.Equal(0, data.TotalCount);
        Assert.Equal(0, data.TotalAmount);
        Assert.Equal(0, data.TotalPages);
    }

    [Fact(DisplayName = "FUNC139-ADM-UTCID01")]
    public async Task Detail_maps_order_without_transaction()
    {
        await using var db = Db();
        var order = Order("DETAIL", "Paid", 55, DateTime.UtcNow);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var detail = Detail(await Controller(db).GetById(order.OrderId, default));

        Assert.Equal(order.OrderCode, detail.OrderCode);
        Assert.Equal(55, detail.Amount);
        Assert.Null(detail.Transaction);
    }

    [Fact(DisplayName = "FUNC139-ADM-UTCID02")]
    public async Task Detail_returns_latest_transaction_and_all_transaction_fields()
    {
        await using var db = Db();
        var order = Order("DETAIL-TX", "Paid", 75, DateTime.UtcNow);
        var latest = Transaction(order, "latest", order.CreatedAt.AddMinutes(2));
        latest.VnpBankCode = "NCB";
        latest.SecureHashValid = true;
        db.Orders.Add(order);
        db.PaymentTransactions.AddRange(Transaction(order, "old", order.CreatedAt), latest);
        await db.SaveChangesAsync();

        var detail = Detail(await Controller(db).GetById(order.OrderId, default));

        Assert.Equal("latest", detail.VnpTransactionNo);
        Assert.Equal(latest.TransactionId, detail.Transaction!.TransactionId);
        Assert.Equal("NCB", detail.Transaction.VnpBankCode);
        Assert.True(detail.Transaction.SecureHashValid);
    }

    [Fact(DisplayName = "FUNC139-ADM-UTCID03")]
    public async Task Detail_returns_not_found_for_unknown_id()
    {
        await using var db = Db();
        var id = Guid.NewGuid();

        var result = Assert.IsType<NotFoundObjectResult>(await Controller(db).GetById(id, default));

        var response = Assert.IsType<ApiResponse<ManagerPaymentDetailDto>>(result.Value);
        Assert.False(response.Success);
        Assert.Contains(id.ToString(), response.Message);
    }

    [Fact(DisplayName = "FUNC139-ADM-UTCID04")]
    public async Task Detail_returns_not_found_for_empty_guid_boundary()
    {
        await using var db = Db();

        var result = await Controller(db).GetById(Guid.Empty, default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact(DisplayName = "FUNC139-ADM-UTCID05")]
    public async Task Detail_honors_pre_cancelled_request()
    {
        await using var db = Db();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Controller(db).GetById(Guid.NewGuid(), cancellation.Token));
    }

    [Fact(DisplayName = "FUNC139-ADM-UTCID06")]
    public async Task Detail_preserves_order_boundary_and_timestamp_fields()
    {
        await using var db = Db();
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var order = Order("BOUNDARY", "Pending", decimal.MaxValue, created);
        order.Credits = int.MaxValue;
        order.ExpiredAt = created.AddMinutes(1);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var detail = Detail(await Controller(db).GetById(order.OrderId, default));

        Assert.Equal(decimal.MaxValue, detail.Amount);
        Assert.Equal(int.MaxValue, detail.Credits);
        Assert.Equal(order.ExpiredAt, detail.ExpiredAt);
        Assert.Equal(order.UpdatedAt, detail.UpdatedAt);
    }

    private static ManagerPaymentListResultDto Data(IActionResult result) =>
        Assert.IsType<ApiResponse<ManagerPaymentListResultDto>>(Assert.IsType<OkObjectResult>(result).Value).Data!;

    private static ManagerPaymentDetailDto Detail(IActionResult result) =>
        Assert.IsType<ApiResponse<ManagerPaymentDetailDto>>(Assert.IsType<OkObjectResult>(result).Value).Data!;

    private static PaymentDbContext Db() => new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static TestableManagerPaymentsController Controller(PaymentDbContext db) => new(db);

    private static Order Order(string code, string status, decimal amount, DateTime created, Guid? user = null, string method = "VNPay") => new()
    {
        OrderId = Guid.NewGuid(), OrderCode = code, UserId = user ?? Guid.NewGuid(), PackageId = Guid.NewGuid(),
        Amount = amount, Credits = 10, Currency = "VND", PaymentMethod = method, Status = status,
        PaidAt = status == "Paid" ? created : null, CreatedAt = created, UpdatedAt = created
    };

    private static PaymentTransaction Transaction(Order order, string number, DateTime created) => new()
    {
        TransactionId = Guid.NewGuid(), OrderId = order.OrderId, Amount = order.Amount,
        VnpTransactionNo = number, VnpResponseCode = "00", Status = "Success", CreatedAt = created
    };

    private sealed class TestableManagerPaymentsController : ManagerPaymentsController
    {
        public TestableManagerPaymentsController(PaymentDbContext db)
            : base(db, NullLogger<ManagerPaymentsController>.Instance,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PAYMENT_DB_CONNECTION_STRING"] = "Server=test;Database=test"
                }).Build()) { }

        protected override Task EnrichWithUserNamesAsync(List<ManagerPaymentListItemDto> items, CancellationToken ct) => Task.CompletedTask;
    }
}

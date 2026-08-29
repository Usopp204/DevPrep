using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using PaymentAPI.Controllers;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Repositories;
using PaymentAPI.Services;
using Xunit;

namespace PaymentAPI.Tests;

public class PaymentTests
{
    [Theory]
    [InlineData(0, 0, 1, 10)]
    [InlineData(-1, 101, 1, 10)]
    [InlineData(2, 20, 2, 20)]
    public async Task Manager_packages_normalizes_pagination_and_returns_activity_counts(int page, int size, int expectedPage, int expectedSize)
    {
        await using var db = Db();
        db.CreditPackages.AddRange(new CreditPackage { PackageId = Guid.NewGuid(), Name = "Active", IsActive = true }, new CreditPackage { PackageId = Guid.NewGuid(), Name = "Inactive", IsActive = false });
        await db.SaveChangesAsync();
        var service = new Mock<ICreditPackageService>();
        service.Setup(x => x.GetListAsync(expectedPage, expectedSize, "credit", null)).ReturnsAsync(new PagedResult<CreditPackageDto> { Page = expectedPage, PageSize = expectedSize });
        var result = Assert.IsType<OkObjectResult>(await new ManagerCreditPackagesController(service.Object, db).GetList(page, size, "credit", null));
        var pageResult = Assert.IsType<ApiResponse<PagedResult<CreditPackageDto>>>(result.Value).Data!;
        Assert.Equal(1, pageResult.ActiveCount); Assert.Equal(1, pageResult.InactiveCount);
        service.Verify(x => x.GetListAsync(expectedPage, expectedSize, "credit", null), Times.Once);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task Manager_package_detail_maps_found_and_missing(bool found, int status)
    {
        await using var db = Db(); var service = new Mock<ICreditPackageService>(); var id = Guid.NewGuid();
        service.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(found ? new CreditPackageDto { PackageId = id, Name = "Starter" } : null);
        var result = await new ManagerCreditPackagesController(service.Object, db).GetById(id);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Last_activity_returns_empty_for_empty_user_list()
    {
        await using var db = Db();
        var result = Assert.IsType<OkObjectResult>(await new InternalPaymentActivityController(db).GetLastActivity(new List<Guid>()));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<object>>(result.Value));
    }

    [Fact]
    public async Task Last_activity_returns_latest_paid_order_per_user()
    {
        await using var db = Db(); var user = Guid.NewGuid(); var package = Guid.NewGuid(); var first = DateTime.UtcNow.AddDays(-2); var latest = DateTime.UtcNow.AddDays(-1);
        db.Orders.AddRange(Order(user, package, "Paid", first), Order(user, package, "Paid", latest), Order(user, package, "Pending", DateTime.UtcNow)); await db.SaveChangesAsync();
        var result = Assert.IsType<OkObjectResult>(await new InternalPaymentActivityController(db).GetLastActivity(new List<Guid> { user }));
        var values = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result.Value).Cast<object>().ToList();
        Assert.Single(values);
        Assert.Equal(user, (Guid)values[0].GetType().GetProperty("UserId")!.GetValue(values[0])!);
        Assert.Equal(latest, (DateTime)values[0].GetType().GetProperty("LastActivityAt")!.GetValue(values[0])!);
    }

    [Fact]
    public async Task Order_repository_returns_order_by_code()
    {
        await using var db = Db(); var order = Order(Guid.NewGuid(), Guid.NewGuid(), "Paid", DateTime.UtcNow); db.Orders.Add(order); await db.SaveChangesAsync();
        Assert.Same(order, await new OrderRepository(db).GetByOrderCodeAsync(order.OrderCode));
    }

    [Fact]
    public async Task Order_repository_excludes_pending_from_user_history()
    {
        await using var db = Db(); var user = Guid.NewGuid(); var package = Guid.NewGuid(); db.Orders.AddRange(Order(user, package, "Paid", DateTime.UtcNow), Order(user, package, "Pending", DateTime.UtcNow)); await db.SaveChangesAsync();
        var result = await new OrderRepository(db).GetPagedByUserAsync(user, 1, 10);
        Assert.Single(result.Items); Assert.Equal("Paid", result.Items[0].Status); Assert.Equal(1, result.TotalCount);
    }

    [Theory]
    [InlineData(1, 2, 2)]
    [InlineData(2, 2, 1)]
    [InlineData(3, 2, 0)]
    public async Task Order_repository_paginates_all_orders(int page, int size, int expectedCount)
    {
        await using var db = Db(); var user = Guid.NewGuid(); var package = Guid.NewGuid();
        db.Orders.AddRange(Order(user, package, "Paid", DateTime.UtcNow), Order(user, package, "Failed", DateTime.UtcNow.AddMinutes(-1)), Order(user, package, "Pending", DateTime.UtcNow.AddMinutes(-2))); await db.SaveChangesAsync();
        var result = await new OrderRepository(db).GetPagedAllAsync(page, size);
        Assert.Equal(expectedCount, result.Items.Count); Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task Order_repository_sums_only_paid_credits()
    {
        await using var db = Db(); var user = Guid.NewGuid(); var package = Guid.NewGuid(); var paid1 = Order(user, package, "Paid", DateTime.UtcNow); paid1.Credits = 10; var paid2 = Order(user, package, "Paid", DateTime.UtcNow); paid2.Credits = 15; var failed = Order(user, package, "Failed", DateTime.UtcNow); failed.Credits = 100; db.Orders.AddRange(paid1, paid2, failed); await db.SaveChangesAsync();
        Assert.Equal(25, await new OrderRepository(db).GetTotalCreditsByUserAsync(user));
    }

    [Fact]
    public async Task Order_repository_returns_zero_credits_without_paid_orders()
    {
        await using var db = Db();
        Assert.Equal(0, await new OrderRepository(db).GetTotalCreditsByUserAsync(Guid.NewGuid()));
    }

    private static PaymentDbContext Db() => new(new DbContextOptionsBuilder<PaymentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Order Order(Guid user, Guid package, string status, DateTime time) => new() { OrderId = Guid.NewGuid(), OrderCode = Guid.NewGuid().ToString("N"), UserId = user, PackageId = package, Amount = 1000, Credits = 10, Status = status, CreatedAt = time, UpdatedAt = time, PaidAt = status == "Paid" ? time : null };
}

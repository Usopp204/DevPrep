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

public class AdminCreditPackageReadUtcidTests
{
    [Fact(DisplayName = "FUNC140-ADM-UTCID01")]
    public async Task List_returns_service_page_and_database_activity_counts()
    {
        await using var db = Db();
        db.CreditPackages.AddRange(Package("Active", true), Package("Inactive", false));
        await db.SaveChangesAsync();
        var service = new Mock<ICreditPackageService>();
        service.Setup(x => x.GetListAsync(1, 10, null, null)).ReturnsAsync(Page("Active"));

        var data = ListData(await Controller(service, db).GetList());

        Assert.Single(data.Items);
        Assert.Equal(1, data.ActiveCount);
        Assert.Equal(1, data.InactiveCount);
    }

    [Fact(DisplayName = "FUNC140-ADM-UTCID02")]
    public async Task List_forwards_keyword_and_active_filter()
    {
        await using var db = Db();
        var service = new Mock<ICreditPackageService>();
        service.Setup(x => x.GetListAsync(2, 25, "pro", false)).ReturnsAsync(Page());

        _ = await Controller(service, db).GetList(2, 25, "pro", false);

        service.Verify(x => x.GetListAsync(2, 25, "pro", false), Times.Once);
    }

    [Fact(DisplayName = "FUNC140-ADM-UTCID03")]
    public async Task List_preserves_empty_result_metadata()
    {
        await using var db = Db();
        var service = new Mock<ICreditPackageService>();
        service.Setup(x => x.GetListAsync(3, 10, null, true)).ReturnsAsync(new PagedResult<CreditPackageDto>
        {
            Page = 3, PageSize = 10, TotalCount = 0
        });

        var data = ListData(await Controller(service, db).GetList(3, 10, null, true));

        Assert.Empty(data.Items);
        Assert.Equal(3, data.Page);
        Assert.Equal(0, data.TotalCount);
    }

    [Fact(DisplayName = "FUNC140-ADM-UTCID04")]
    public async Task List_normalizes_non_positive_page()
    {
        await using var db = Db();
        var service = new Mock<ICreditPackageService>();
        service.Setup(x => x.GetListAsync(1, 10, null, null)).ReturnsAsync(Page());

        _ = await Controller(service, db).GetList(-100, 10);

        service.Verify(x => x.GetListAsync(1, 10, null, null), Times.Once);
    }

    [Fact(DisplayName = "FUNC140-ADM-UTCID05")]
    public async Task List_normalizes_page_size_outside_boundaries()
    {
        await using var db = Db();
        var service = new Mock<ICreditPackageService>();
        service.Setup(x => x.GetListAsync(1, 10, null, null)).ReturnsAsync(Page());

        _ = await Controller(service, db).GetList(1, 0);
        _ = await Controller(service, db).GetList(1, 101);

        service.Verify(x => x.GetListAsync(1, 10, null, null), Times.Exactly(2));
    }

    [Fact(DisplayName = "FUNC140-ADM-UTCID06")]
    public async Task List_propagates_service_failure()
    {
        await using var db = Db();
        var service = new Mock<ICreditPackageService>();
        service.Setup(x => x.GetListAsync(1, 10, null, null)).ThrowsAsync(new InvalidOperationException("storage unavailable"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Controller(service, db).GetList());

        Assert.Equal("storage unavailable", error.Message);
    }

    [Fact(DisplayName = "FUNC141-ADM-UTCID01")]
    public async Task Detail_maps_every_persisted_field_through_real_service()
    {
        var repo = new Mock<ICreditPackageRepository>();
        var package = Package("Professional", true);
        package.Description = "Full package";
        package.BonusCredits = 7;
        package.Currency = "USD";
        repo.Setup(x => x.GetByIdAsync(package.PackageId)).ReturnsAsync(package);
        await using var db = Db();

        var data = DetailData(await new ManagerCreditPackagesController(new CreditPackageService(repo.Object), db).GetById(package.PackageId));

        Assert.Equal(package.PackageId, data.PackageId);
        Assert.Equal(package.Description, data.Description);
        Assert.Equal(7, data.BonusCredits);
        Assert.Equal("USD", data.Currency);
    }

    [Fact(DisplayName = "FUNC141-ADM-UTCID02")]
    public async Task Detail_returns_inactive_package_for_admin()
    {
        await using var db = Db();
        var service = new Mock<ICreditPackageService>();
        var id = Guid.NewGuid();
        service.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(new CreditPackageDto { PackageId = id, IsActive = false });

        var data = DetailData(await Controller(service, db).GetById(id));

        Assert.False(data.IsActive);
    }

    [Fact(DisplayName = "FUNC141-ADM-UTCID03")]
    public async Task Detail_returns_not_found_with_requested_id()
    {
        await using var db = Db();
        var service = new Mock<ICreditPackageService>();
        var id = Guid.NewGuid();

        var result = Assert.IsType<NotFoundObjectResult>(await Controller(service, db).GetById(id));

        var response = Assert.IsType<ApiResponse<CreditPackageDto>>(result.Value);
        Assert.False(response.Success);
        Assert.Contains(id.ToString(), response.Message);
    }

    [Fact(DisplayName = "FUNC141-ADM-UTCID04")]
    public async Task Detail_empty_guid_is_forwarded_and_returns_not_found()
    {
        await using var db = Db();
        var service = new Mock<ICreditPackageService>();

        Assert.IsType<NotFoundObjectResult>(await Controller(service, db).GetById(Guid.Empty));
        service.Verify(x => x.GetByIdAsync(Guid.Empty), Times.Once);
    }

    [Fact(DisplayName = "FUNC141-ADM-UTCID05")]
    public async Task Detail_preserves_numeric_boundaries()
    {
        await using var db = Db();
        var service = new Mock<ICreditPackageService>();
        var id = Guid.NewGuid();
        service.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(new CreditPackageDto
        {
            PackageId = id, Credits = int.MaxValue, BonusCredits = 0, Price = decimal.MaxValue, SortOrder = int.MinValue
        });

        var data = DetailData(await Controller(service, db).GetById(id));

        Assert.Equal(int.MaxValue, data.Credits);
        Assert.Equal(decimal.MaxValue, data.Price);
        Assert.Equal(int.MinValue, data.SortOrder);
    }

    [Fact(DisplayName = "FUNC141-ADM-UTCID06")]
    public async Task Detail_propagates_repository_failure()
    {
        await using var db = Db();
        var service = new Mock<ICreditPackageService>();
        var id = Guid.NewGuid();
        service.Setup(x => x.GetByIdAsync(id)).ThrowsAsync(new TimeoutException("database timeout"));

        var error = await Assert.ThrowsAsync<TimeoutException>(() => Controller(service, db).GetById(id));

        Assert.Equal("database timeout", error.Message);
    }

    private static ManagerCreditPackagesController Controller(Mock<ICreditPackageService> service, PaymentDbContext db) => new(service.Object, db);
    private static PagedResult<CreditPackageDto> ListData(IActionResult result) => Assert.IsType<ApiResponse<PagedResult<CreditPackageDto>>>(Assert.IsType<OkObjectResult>(result).Value).Data!;
    private static CreditPackageDto DetailData(IActionResult result) => Assert.IsType<ApiResponse<CreditPackageDto>>(Assert.IsType<OkObjectResult>(result).Value).Data!;
    private static PagedResult<CreditPackageDto> Page(string? name = null) => new() { Items = name is null ? [] : [new CreditPackageDto { Name = name }], Page = 1, PageSize = 10 };
    private static PaymentDbContext Db() => new(new DbContextOptionsBuilder<PaymentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static CreditPackage Package(string name, bool active) => new()
    {
        PackageId = Guid.NewGuid(), Name = name, Credits = 100, Price = 50, IsActive = active,
        SortOrder = 1, CreatedAt = DateTime.UtcNow.AddDays(-1), UpdatedAt = DateTime.UtcNow
    };
}

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Moq;
using PaymentAPI.Endpoints;
using PaymentAPI.Models;
using PaymentAPI.Repositories;
using Xunit;

namespace PaymentAPI.Tests;

public class AdminCreditPackageWriteUtcidTests
{
    [Fact(DisplayName = "FUNC142-ADM-UTCID01")]
    public async Task Add_creates_active_package_and_saves_all_fields()
    {
        var repo = Repo();
        CreditPackage? captured = null;
        repo.Setup(x => x.AddAsync(It.IsAny<CreditPackage>())).Callback<CreditPackage>(x => captured = x).Returns(Task.CompletedTask);
        repo.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        var before = DateTime.UtcNow.AddHours(7);

        var result = await CreditPackageEndpoints.AddAsync(Context(Request("Pro", "Description", 100, 20, 99, "USD", 3)), repo.Object);

        Assert.Equal(201, Status(result));
        Assert.NotNull(captured);
        Assert.Equal("Pro", captured.Name);
        Assert.Equal("Description", captured.Description);
        Assert.Equal((100, 20, 99m, "USD", 3), (captured.Credits, captured.BonusCredits, captured.Price, captured.Currency, captured.SortOrder));
        Assert.True(captured.IsActive);
        Assert.True(captured.CreatedAt >= before);
        Assert.Equal(captured.CreatedAt, captured.UpdatedAt);
    }

    [Fact(DisplayName = "FUNC142-ADM-UTCID02")]
    public async Task Add_defaults_null_currency_to_vnd()
    {
        var repo = Repo();
        CreditPackage? captured = null;
        repo.Setup(x => x.AddAsync(It.IsAny<CreditPackage>())).Callback<CreditPackage>(x => captured = x).Returns(Task.CompletedTask);

        Assert.Equal(201, Status(await CreditPackageEndpoints.AddAsync(Context(Request(currency: null)), repo.Object)));
        Assert.Equal("VND", captured!.Currency);
    }

    [Fact(DisplayName = "FUNC142-ADM-UTCID03")]
    public async Task Add_rejects_duplicate_active_name_without_mutation()
    {
        var repo = Repo();
        repo.Setup(x => x.ExistsActiveNameAsync("Duplicate", null)).ReturnsAsync(true);

        var result = await CreditPackageEndpoints.AddAsync(Context(Request("Duplicate")), repo.Object);

        Assert.Equal(409, Status(result));
        repo.Verify(x => x.AddAsync(It.IsAny<CreditPackage>()), Times.Never);
        repo.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact(DisplayName = "FUNC142-ADM-UTCID04")]
    public async Task Add_rejects_malformed_json()
    {
        var repo = Repo();
        var result = await CreditPackageEndpoints.AddAsync(Context("{not-json"), repo.Object);
        Assert.Equal(400, Status(result));
        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "FUNC142-ADM-UTCID05")]
    public async Task Add_preserves_numeric_boundary_values()
    {
        var repo = Repo();
        CreditPackage? captured = null;
        repo.Setup(x => x.AddAsync(It.IsAny<CreditPackage>())).Callback<CreditPackage>(x => captured = x).Returns(Task.CompletedTask);

        var result = await CreditPackageEndpoints.AddAsync(Context(Request("Boundary", null, int.MaxValue, 0, decimal.MaxValue, null, int.MinValue)), repo.Object);

        Assert.Equal(201, Status(result));
        Assert.Equal(int.MaxValue, captured!.Credits);
        Assert.Equal(decimal.MaxValue, captured.Price);
        Assert.Equal(int.MinValue, captured.SortOrder);
    }

    [Fact(DisplayName = "FUNC142-ADM-UTCID06")]
    public async Task Add_propagates_save_failure()
    {
        var repo = Repo();
        repo.Setup(x => x.AddAsync(It.IsAny<CreditPackage>())).Returns(Task.CompletedTask);
        repo.Setup(x => x.SaveChangesAsync()).ThrowsAsync(new InvalidOperationException("write failed"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreditPackageEndpoints.AddAsync(Context(Request()), repo.Object));
        Assert.Equal("write failed", error.Message);
    }

    [Fact(DisplayName = "FUNC143-ADM-UTCID01")]
    public async Task Update_mutates_all_editable_fields_and_saves()
    {
        var package = Package();
        var repo = Repo();
        repo.Setup(x => x.GetByIdAsync(package.PackageId)).ReturnsAsync(package);
        var before = DateTime.UtcNow.AddHours(7);

        var result = await CreditPackageEndpoints.UpdateAsync(package.PackageId, Context(Request("Updated", "New", 22, 3, 44, "USD", 8)), repo.Object);

        Assert.Equal(200, Status(result));
        Assert.Equal(("Updated", "New", 22, 3, 44m, "USD", 8), (package.Name, package.Description, package.Credits, package.BonusCredits, package.Price, package.Currency, package.SortOrder));
        Assert.True(package.UpdatedAt >= before);
        repo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact(DisplayName = "FUNC143-ADM-UTCID02")]
    public async Task Update_null_currency_preserves_existing_currency()
    {
        var package = Package(); package.Currency = "EUR";
        var repo = Repo(); repo.Setup(x => x.GetByIdAsync(package.PackageId)).ReturnsAsync(package);
        Assert.Equal(200, Status(await CreditPackageEndpoints.UpdateAsync(package.PackageId, Context(Request(currency: null)), repo.Object)));
        Assert.Equal("EUR", package.Currency);
    }

    [Fact(DisplayName = "FUNC143-ADM-UTCID03")]
    public async Task Update_returns_not_found_before_reading_body()
    {
        var repo = Repo(); var id = Guid.NewGuid();
        Assert.Equal(404, Status(await CreditPackageEndpoints.UpdateAsync(id, Context("invalid"), repo.Object)));
        repo.Verify(x => x.ExistsActiveNameAsync(It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact(DisplayName = "FUNC143-ADM-UTCID04")]
    public async Task Update_rejects_malformed_json_without_saving()
    {
        var package = Package(); var repo = Repo(); repo.Setup(x => x.GetByIdAsync(package.PackageId)).ReturnsAsync(package);
        Assert.Equal(400, Status(await CreditPackageEndpoints.UpdateAsync(package.PackageId, Context("{"), repo.Object)));
        repo.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact(DisplayName = "FUNC143-ADM-UTCID05")]
    public async Task Update_excludes_self_from_duplicate_check_and_rejects_collision()
    {
        var package = Package(); var repo = Repo(); repo.Setup(x => x.GetByIdAsync(package.PackageId)).ReturnsAsync(package);
        repo.Setup(x => x.ExistsActiveNameAsync("Taken", package.PackageId)).ReturnsAsync(true);
        Assert.Equal(409, Status(await CreditPackageEndpoints.UpdateAsync(package.PackageId, Context(Request("Taken")), repo.Object)));
        repo.Verify(x => x.ExistsActiveNameAsync("Taken", package.PackageId), Times.Once);
        repo.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact(DisplayName = "FUNC143-ADM-UTCID06")]
    public async Task Update_propagates_save_failure()
    {
        var package = Package(); var repo = Repo(); repo.Setup(x => x.GetByIdAsync(package.PackageId)).ReturnsAsync(package);
        repo.Setup(x => x.SaveChangesAsync()).ThrowsAsync(new TimeoutException("timeout"));
        await Assert.ThrowsAsync<TimeoutException>(() => CreditPackageEndpoints.UpdateAsync(package.PackageId, Context(Request()), repo.Object));
    }

    [Fact(DisplayName = "FUNC144-ADM-UTCID01")]
    public async Task Delete_soft_deletes_active_package()
    {
        var package = Package(); var repo = Repo(); repo.Setup(x => x.GetByIdAsync(package.PackageId)).ReturnsAsync(package);
        Assert.Equal(204, Status(await CreditPackageEndpoints.DeleteAsync(package.PackageId, repo.Object)));
        Assert.False(package.IsActive);
        repo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact(DisplayName = "FUNC144-ADM-UTCID02")]
    public async Task Delete_is_idempotent_for_inactive_package()
    {
        var package = Package(); package.IsActive = false; var repo = Repo(); repo.Setup(x => x.GetByIdAsync(package.PackageId)).ReturnsAsync(package);
        Assert.Equal(204, Status(await CreditPackageEndpoints.DeleteAsync(package.PackageId, repo.Object)));
        Assert.False(package.IsActive);
    }

    [Fact(DisplayName = "FUNC144-ADM-UTCID03")]
    public async Task Delete_returns_not_found_without_saving()
    {
        var repo = Repo();
        Assert.Equal(404, Status(await CreditPackageEndpoints.DeleteAsync(Guid.NewGuid(), repo.Object)));
        repo.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact(DisplayName = "FUNC144-ADM-UTCID04")]
    public async Task Delete_empty_guid_boundary_returns_not_found()
    {
        var repo = Repo();
        Assert.Equal(404, Status(await CreditPackageEndpoints.DeleteAsync(Guid.Empty, repo.Object)));
        repo.Verify(x => x.GetByIdAsync(Guid.Empty), Times.Once);
    }

    [Fact(DisplayName = "FUNC144-ADM-UTCID05")]
    public async Task Delete_updates_timestamp_to_application_timezone()
    {
        var package = Package(); var repo = Repo(); repo.Setup(x => x.GetByIdAsync(package.PackageId)).ReturnsAsync(package);
        var before = DateTime.UtcNow.AddHours(7);
        await CreditPackageEndpoints.DeleteAsync(package.PackageId, repo.Object);
        Assert.True(package.UpdatedAt >= before);
    }

    [Fact(DisplayName = "FUNC144-ADM-UTCID06")]
    public async Task Delete_propagates_save_failure()
    {
        var package = Package(); var repo = Repo(); repo.Setup(x => x.GetByIdAsync(package.PackageId)).ReturnsAsync(package);
        repo.Setup(x => x.SaveChangesAsync()).ThrowsAsync(new InvalidOperationException("delete failed"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreditPackageEndpoints.DeleteAsync(package.PackageId, repo.Object));
        Assert.Equal("delete failed", error.Message);
    }

    private static Mock<ICreditPackageRepository> Repo()
    {
        var repo = new Mock<ICreditPackageRepository>();
        repo.Setup(x => x.ExistsActiveNameAsync(It.IsAny<string>(), It.IsAny<Guid?>())).ReturnsAsync(false);
        repo.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        return repo;
    }

    private static DefaultHttpContext Context(string json)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return context;
    }

    private static string Request(string name = "Starter", string? description = null, int credits = 10, int bonus = 1, decimal price = 20, string? currency = "VND", int sort = 1) =>
        JsonSerializer.Serialize(new AdminPackageRequest(name, description, credits, bonus, price, currency, sort));

    private static int Status(IResult result) => Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode!.Value;
    private static CreditPackage Package() => new() { PackageId = Guid.NewGuid(), Name = "Old", Credits = 1, Price = 2, Currency = "VND", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-1), UpdatedAt = DateTime.UtcNow.AddDays(-1) };
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Admin;
using WebClient.Services.Admin;

namespace WebClient.Controllers.Admin;

// Admin only. The guard this replaces also let Manager through, which is why a Manager
// signing in landed on the Admin credit-package page; Manager has its own screens at
// /Manager/CreditPackages and /Manager/Payments (ManagerController), so nothing is lost.
//
// The role now comes from the claim inside the signed authentication cookie rather than
// from the user_role cookie, which is written with HttpOnly = false and can therefore be
// edited in the browser.
[Authorize(Roles = "Admin")]
[Route("Admin/Payment")]
public class PaymentAdminController(IPaymentAdminService adminService) : Controller
{
    [HttpGet("")]
    [HttpGet("Packages")]
    public async Task<IActionResult> Packages(string? success, string? error, CancellationToken ct = default)
    {
        var model = await adminService.GetPackagesAsync(ct);
        if (success != null) model.SuccessMessage = success;
        if (error != null) model.ErrorMessage = error;
        return View(model);
    }

    [HttpPost("Packages/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePackage(
        string name, string? description, int credits, int bonusCredits,
        decimal price, int sortOrder, CancellationToken ct = default)
    {
        var dto = new CreditPackageAdminDto
        {
            Name = name, Description = description, Credits = credits,
            BonusCredits = bonusCredits, Price = price, SortOrder = sortOrder
        };
        var (ok, err) = await adminService.CreatePackageAsync(dto, ct);
        return ok
            ? RedirectToAction(nameof(Packages), new { success = $"Package '{name}' created." })
            : RedirectToAction(nameof(Packages), new { error = err ?? "Failed to create package." });
    }

    [HttpPost("Packages/Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPackage(
        Guid id, string name, string? description, int credits,
        int bonusCredits, decimal price, int sortOrder, CancellationToken ct = default)
    {
        var dto = new CreditPackageAdminDto
        {
            Name = name, Description = description, Credits = credits,
            BonusCredits = bonusCredits, Price = price, SortOrder = sortOrder
        };
        var (ok, err) = await adminService.UpdatePackageAsync(id, dto, ct);
        return ok
            ? RedirectToAction(nameof(Packages), new { success = $"Package '{name}' updated." })
            : RedirectToAction(nameof(Packages), new { error = err ?? "Failed to update package." });
    }

    [HttpPost("Packages/Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePackage(Guid id, string? name, CancellationToken ct = default)
    {
        var (ok, err) = await adminService.DeletePackageAsync(id, ct);
        return ok
            ? RedirectToAction(nameof(Packages), new { success = $"Package '{name}' deactivated." })
            : RedirectToAction(nameof(Packages), new { error = err ?? "Failed to delete package." });
    }

    [HttpPost("Packages/Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestorePackage(Guid id, string? name, CancellationToken ct = default)
    {
        var (ok, err) = await adminService.RestorePackageAsync(id, ct);
        return ok
            ? RedirectToAction(nameof(Packages), new { success = $"Package '{name}' restored." })
            : RedirectToAction(nameof(Packages), new { error = err ?? "Failed to restore package." });
    }

    [HttpGet("Payments")]
    public async Task<IActionResult> Payments(
        int page = 1, string? keyword = null, string? status = null, string? paymentMethod = null,
        DateTime? dateFrom = null, DateTime? dateTo = null,
        decimal? minAmount = null, decimal? maxAmount = null,
        string period = "today", DateTime? from = null, DateTime? to = null,
        CancellationToken ct = default)
    {

        var model = await adminService.GetPaymentsAsync(
            page, keyword: keyword, status: status, paymentMethod: paymentMethod,
            dateFrom: dateFrom, dateTo: dateTo, minAmount: minAmount, maxAmount: maxAmount, ct: ct);

        model.Revenue = await adminService.GetRevenueReportAsync(period, from, to, ct);

        return View(model);
    }

    // JSON feed for the transaction-detail modal on the Payments page. The class-level
    // [Authorize] covers it: a fetch() that fails authorisation is answered with a 401 and a
    // JSON body rather than a redirect to the login page, which would have arrived here as
    // login HTML with a 200 status. See DenyOrRedirect in ServiceCollectionExtensions.
    [HttpGet("Payments/Detail/{id:guid}")]
    public async Task<IActionResult> PaymentDetail(Guid id, CancellationToken ct = default)
    {
        var detail = await adminService.GetPaymentDetailAsync(id, ct);
        return detail?.Order is null
            ? NotFound(new { error = "Transaction not found." })
            : Json(detail);
    }
}

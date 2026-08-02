using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Admin;
using WebClient.Services.Admin;

namespace WebClient.Controllers.Admin;

[Route("Admin/Payment")]
public class PaymentAdminController(IPaymentAdminService adminService) : Controller
{
    private IActionResult? GuardAdmin()
    {
        if (string.IsNullOrEmpty(Request.Cookies["access_token"]))
            return RedirectToAction("Login", "Auth");
        var role = Request.Cookies["user_role"] ?? string.Empty;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Login", "Auth");
        return null;
    }

    [HttpGet("")]
    [HttpGet("Packages")]
    public async Task<IActionResult> Packages(string? success, string? error, CancellationToken ct = default)
    {
        if (GuardAdmin() is { } r1) return r1;
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
        if (GuardAdmin() is { } r2) return r2;
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
        if (GuardAdmin() is { } r3) return r3;
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
        if (GuardAdmin() is { } r4) return r4;
        var (ok, err) = await adminService.DeletePackageAsync(id, ct);
        return ok
            ? RedirectToAction(nameof(Packages), new { success = $"Package '{name}' deactivated." })
            : RedirectToAction(nameof(Packages), new { error = err ?? "Failed to delete package." });
    }

    [HttpPost("Packages/Restore/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestorePackage(Guid id, string? name, CancellationToken ct = default)
    {
        if (GuardAdmin() is { } r6) return r6;
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
        if (GuardAdmin() is { } r5) return r5;

        var model = await adminService.GetPaymentsAsync(
            page, keyword: keyword, status: status, paymentMethod: paymentMethod,
            dateFrom: dateFrom, dateTo: dateTo, minAmount: minAmount, maxAmount: maxAmount, ct: ct);

        model.Revenue = await adminService.GetRevenueReportAsync(period, from, to, ct);

        return View(model);
    }

    // JSON feed for the transaction-detail modal on the Payments page. Deliberately does NOT
    // use GuardAdmin(): that redirects to the login page, and fetch() would then receive the
    // login HTML with a 200 status instead of an error it can act on.
    [HttpGet("Payments/Detail/{id:guid}")]
    public async Task<IActionResult> PaymentDetail(Guid id, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(Request.Cookies["access_token"]))
            return Unauthorized(new { error = "Your session has expired. Please sign in again." });

        var role = Request.Cookies["user_role"] ?? string.Empty;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Admin access required." });

        var detail = await adminService.GetPaymentDetailAsync(id, ct);
        return detail?.Order is null
            ? NotFound(new { error = "Transaction not found." })
            : Json(detail);
    }
}

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

    [HttpGet("Payments")]
    public async Task<IActionResult> Payments(int page = 1, CancellationToken ct = default)
    {
        if (GuardAdmin() is { } r5) return r5;
        var model = await adminService.GetPaymentsAsync(page, ct: ct);
        return View(model);
    }
}

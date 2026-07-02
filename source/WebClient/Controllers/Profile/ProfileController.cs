using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Profile;
using WebClient.Services.Payment;
using WebClient.Services.Profile;

namespace WebClient.Controllers.Profile;

public class ProfileController(
    IProfileService profileService,
    IPaymentService paymentService) : Controller
{
    // ============================ View Profile ============================
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await profileService.GetMyProfileAsync();
        if (result.Unauthorized)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (!result.Success || result.Profile is null)
        {
            TempData["Error"] = result.Message;
            return View(new ProfileViewModel());
        }

        return View(result.Profile);
    }

    // ============================ Edit Profile ============================
    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var result = await profileService.GetMyProfileAsync();
        if (result.Unauthorized)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (!result.Success || result.Profile is null)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        return View(new EditProfileViewModel
        {
            FullName = result.Profile.FullName ?? string.Empty,
            Email = result.Profile.Email,
            AvatarUrl = result.Profile.AvatarUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await profileService.UpdateMyProfileAsync(model);
        if (result.Unauthorized)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (result.Success)
        {
            TempData["Message"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, result.Message);
        return View(model);
    }

    // ============================ Change Password ============================
    [HttpGet]
    public IActionResult ChangePassword()
    {
        if (string.IsNullOrEmpty(Request.Cookies["access_token"]))
        {
            return RedirectToAction("Login", "Auth");
        }

        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await profileService.ChangePasswordAsync(model);
        if (result.Unauthorized)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (result.Success)
        {
            TempData["Message"] = result.Message;
            return RedirectToAction(nameof(ChangePassword));
        }

        ModelState.AddModelError(string.Empty, result.Message);
        return View(model);
    }

    // ============================ Transaction History ============================
    public async Task<IActionResult> TransactionHistory(int page = 1, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(Request.Cookies["access_token"]))
            return RedirectToAction("Login", "Auth");

        var txRole = Request.Cookies["user_role"] ?? string.Empty;
        if (string.Equals(txRole, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(txRole, "Manager", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "Admin");

        var userIdStr = Request.Cookies["user_id"];
        if (!Guid.TryParse(userIdStr, out var userId))
            return RedirectToAction("Login", "Auth");

        var model = await paymentService.GetTransactionHistoryAsync(userId, page, ct: ct);
        return View(model);
    }
}

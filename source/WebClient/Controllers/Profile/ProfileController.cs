using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Profile;
using WebClient.Services.Profile;

namespace WebClient.Controllers.Profile;

public class ProfileController(IProfileService profileService) : Controller
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
}

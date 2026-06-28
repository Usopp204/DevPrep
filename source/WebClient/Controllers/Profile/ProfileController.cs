using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Profile;

namespace WebClient.Controllers.Profile;

public class ProfileController(IProfileService profileService) : Controller
{
    public IActionResult Index()
    {
        return View(profileService.GetOverview());
    }
}

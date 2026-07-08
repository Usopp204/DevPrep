using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Shared;

namespace WebClient.Controllers.Home;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View(new ModulePageViewModel
        {
            ModuleName = "DevPrep WebClient",
            Epic = "DevPrep",
            Summary = "ASP.NET Core MVC WebClient placeholder."
        });
    }

    public IActionResult Error()
    {
        return View("~/Views/Shared/Error.cshtml", new ModulePageViewModel
        {
            ModuleName = "Error",
            Epic = "Shared",
            Summary = "The request could not be completed."
        });
    }
}

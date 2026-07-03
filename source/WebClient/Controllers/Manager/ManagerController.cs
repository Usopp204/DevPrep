using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Manager;

namespace WebClient.Controllers.Manager;

public class ManagerController(IManagerService managerService) : Controller
{
    public IActionResult Index()
    {
        return View(managerService.GetOverview());
    }

    public IActionResult Feedback()
    {
        return View();
    }
}

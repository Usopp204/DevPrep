using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Admin;

namespace WebClient.Controllers.Admin;

public class AdminController(IAdminService adminService) : Controller
{
    public IActionResult Index()
    {
        return View(adminService.GetOverview());
    }
}

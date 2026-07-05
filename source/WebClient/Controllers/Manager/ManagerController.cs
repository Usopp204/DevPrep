using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Manager;

namespace WebClient.Controllers.Manager;

[Authorize(Roles = "Manager,Admin")]
public class ManagerController(IManagerService managerService) : Controller
{
    public IActionResult Index()
    {
        return View(managerService.GetOverview());
    }

    public IActionResult Feedback()
    {
        ViewData["FeedbackTitle"] = "System Feedback";
        ViewData["FeedbackKind"] = "system";
        ViewData["FeedbackApiBase"] = "http://localhost:7000/api/admin/manager/feedbacks";
        return View();
    }

    public IActionResult AiFeedback()
    {
        ViewData["FeedbackTitle"] = "AI Feedback";
        ViewData["FeedbackKind"] = "ai";
        ViewData["FeedbackApiBase"] = "http://localhost:7000/api/admin/feedbacks/ai";
        return View("Feedback");
    }

    public IActionResult Sessions()
    {
        return View();
    }

    public IActionResult Payments()
    {
        return View();
    }

    public IActionResult CreditPackages()
    {
        return View();
    }
}

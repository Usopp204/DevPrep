using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebClient.Services.Manager;
using WebClient.Services.Shared;

namespace WebClient.Controllers.Manager;

// Manager-only area (Admin not allowed).
[Authorize(Roles = "Manager")]
public class ManagerController(
    IManagerService managerService,
    IOptions<ApiGatewayOptions> gatewayOptions) : Controller
{
    private string ApiBaseUrl => gatewayOptions.Value.BaseUrl?.TrimEnd('/') ?? "http://localhost:7000";

    public IActionResult Index()
    {
        return View(managerService.GetOverview());
    }

    public IActionResult Feedback()
    {
        ViewData["ApiBaseUrl"] = ApiBaseUrl;
        ViewData["FeedbackTitle"] = "System Feedback";
        ViewData["FeedbackKind"] = "system";
        ViewData["FeedbackApiBase"] = $"{ApiBaseUrl}/api/admin/manager/feedbacks";
        return View();
    }

    public IActionResult AiFeedback()
    {
        ViewData["ApiBaseUrl"] = ApiBaseUrl;
        ViewData["FeedbackTitle"] = "AI Feedback";
        ViewData["FeedbackKind"] = "ai";
        ViewData["FeedbackApiBase"] = $"{ApiBaseUrl}/api/admin/feedbacks/ai";
        return View("Feedback");
    }

    public IActionResult Sessions()
    {
        return RedirectToAction(nameof(InterviewSessions));
    }

    public IActionResult Interview()
    {
        return RedirectToAction(nameof(InterviewSessions));
    }

    public IActionResult InterviewSessions()
    {
        ViewData["ApiBaseUrl"] = ApiBaseUrl;
        return View();
    }

    public IActionResult Payments()
    {
        ViewData["ApiBaseUrl"] = ApiBaseUrl;
        return View();
    }

    public IActionResult CreditPackages()
    {
        ViewData["ApiBaseUrl"] = ApiBaseUrl;
        return View();
    }
}
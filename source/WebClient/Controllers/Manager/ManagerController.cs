using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebClient.Services.Manager;
using WebClient.Services.Shared;

namespace WebClient.Controllers.Manager;

// Manager-only area (Admin not allowed).
[Authorize(Roles = "Manager")]
public class ManagerController(
    IOptions<ApiGatewayOptions> gatewayOptions,
    IManagerDashboardService dashboardService) : Controller
{
    private string ApiBaseUrl => gatewayOptions.Value.BaseUrl?.TrimEnd('/') ?? "http://localhost:7000";

    public async Task<IActionResult> Index()
    {
        var model = await dashboardService.GetStatsAsync(HttpContext.RequestAborted);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> RevenueSeries(DateTime? from, DateTime? to)
    {
        var nowVn = DateTime.UtcNow.AddHours(7).Date;
        var f = (from ?? new DateTime(nowVn.Year, nowVn.Month, 1)).Date;
        var t = (to ?? nowVn).Date;

        var result = await dashboardService.GetRevenueDailyAsync(f, t, HttpContext.RequestAborted);
        if (result is null)
            return StatusCode(502, new { success = false, message = "Cannot reach PaymentAPI. Make sure the gateway and PaymentAPI are running." });

        return Json(new { success = true, data = result });
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

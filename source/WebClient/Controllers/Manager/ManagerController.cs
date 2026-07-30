using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebClient.Models.Feedback;
using WebClient.Services.Feedback;
using WebClient.Services.Manager;
using WebClient.Services.Shared;

namespace WebClient.Controllers.Manager;

// Manager-only area (Admin not allowed).
[Authorize(Roles = "Manager")]
public class ManagerController(
    IOptions<ApiGatewayOptions> gatewayOptions,
    IFeedbackService feedbackService,
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

    [HttpGet]
    public async Task<IActionResult> ReportedFeedback(
        int page = 1,
        string feedbackType = "All",
        string status = FeedbackReportRules.PendingStatus,
        string reasonCode = "All",
        string keyword = "",
        Guid? reportId = null,
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 20;
        var list = await feedbackService.GetFeedbackReportsAsync(
            manager: true,
            page,
            pageSize,
            feedbackType,
            status,
            reasonCode,
            keyword,
            cancellationToken);
        var model = new ReportedFeedbackPageViewModel
        {
            Reports = list.Data ?? new WebClient.Models.Shared.PagedResult<FeedbackReportListItemDto>
            {
                Page = Math.Max(1, page),
                PageSize = pageSize
            },
            FeedbackType = feedbackType,
            Status = status,
            ReasonCode = reasonCode,
            Keyword = keyword,
            IsAdmin = false,
            ErrorMessage = list.Success ? null : list.Error
        };

        if (reportId.HasValue)
        {
            var detail = await feedbackService.GetFeedbackReportDetailsAsync(
                manager: true,
                reportId.Value,
                cancellationToken);
            if (detail.Success)
            {
                model.SelectedReport = detail.Data;
            }
            else
            {
                model.ErrorMessage ??= detail.Error;
            }
        }

        return View(model);
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

    [HttpGet("Manager/InterviewSessionDetail/{id:guid}")]
    public IActionResult InterviewSessionDetail(Guid id)
    {
        ViewData["ApiBaseUrl"] = ApiBaseUrl;
        ViewBag.SessionId = id;
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

    public IActionResult Positions()
    {
        ViewData["ApiBaseUrl"] = ApiBaseUrl;
        return View();
    }
}

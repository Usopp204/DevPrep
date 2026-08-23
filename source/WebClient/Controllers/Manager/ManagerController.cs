using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebClient.Models.Feedback;
using WebClient.Services.Feedback;
using WebClient.Services.Manager;
using WebClient.Models.Dashboard;
using WebClient.Services.Dashboard;
using WebClient.Services.Shared;

namespace WebClient.Controllers.Manager;

// Manager-only area (Admin not allowed).
[Authorize(Roles = "Manager")]
public class ManagerController(
    IOptions<ApiGatewayOptions> gatewayOptions,
    IFeedbackService feedbackService,
    IManagementDashboardService dashboardService) : Controller
{
    // Browser-facing on purpose: this value is printed into the pages' fetch() calls, so it has
    // to be the gateway's PUBLISHED address, never the compose DNS name BaseUrl carries inside
    // Docker. Falling back to BaseUrl keeps non-Docker runs working, where the two are equal.
    private string ApiBaseUrl =>
        (string.IsNullOrWhiteSpace(gatewayOptions.Value.PublicBaseUrl)
            ? gatewayOptions.Value.BaseUrl
            : gatewayOptions.Value.PublicBaseUrl)?.TrimEnd('/')
        ?? "http://localhost:7000";

    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var from = new DateOnly(today.Year, today.Month, 1);
        var result = await dashboardService.GetDashboardAsync(from, today, HttpContext.RequestAborted);
        var model = new ManagementDashboardViewModel
        {
            Dashboard = result.Data ?? ManagementDashboardData.Unavailable(from, today),
            DashboardDataUrl = Url.Action(nameof(DashboardData), "Manager") ?? "/Manager/DashboardData"
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DashboardData(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (!DashboardDateRange.TryResolve(from, to, today, out var rangeFrom, out var rangeTo, out var error))
            return BadRequest(new { success = false, message = error });

        var result = await dashboardService.GetDashboardAsync(rangeFrom, rangeTo, HttpContext.RequestAborted);
        if (!result.Success || result.Data is null)
        {
            var status = result.StatusCode is 400 or 401 or 403
                ? result.StatusCode
                : StatusCodes.Status502BadGateway;
            return StatusCode(status, new
            {
                success = false,
                message = result.Error ?? "Could not load dashboard statistics."
            });
        }

        return Json(new { success = true, data = result.Data });
    }
    public IActionResult Announcements()
    {
        ViewData["ApiBaseUrl"] = ApiBaseUrl;
        return View();
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
        string status = "All",
        string reasonCode = "All",
        string keyword = "",
        Guid? reportId = null,
        CancellationToken cancellationToken = default)
    {
        feedbackType = string.IsNullOrWhiteSpace(feedbackType) ? "All" : feedbackType;
        status = string.IsNullOrWhiteSpace(status) ? "All" : status;
        reasonCode = string.IsNullOrWhiteSpace(reasonCode) ? "All" : reasonCode;
        keyword = keyword?.Trim() ?? string.Empty;

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

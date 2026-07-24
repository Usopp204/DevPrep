using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Feedback;
using WebClient.Services.Admin;
using WebClient.Services.Feedback;

namespace WebClient.Controllers.Admin;

public class AdminController(
    IAdminService adminService,
    IAdminDashboardService dashboardService,
    IFeedbackService feedbackService) : Controller
{
    // Claim role trong JWT do AuthAPI phát (chuẩn ClaimTypes.Role).
    private const string RoleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    public async Task<IActionResult> Index()
    {
        // Chỉ tài khoản Admin mới vào được khu Admin (Manager KHÔNG được vào).
        if (!IsAdminRole())
        {
            TempData["Error"] = "You must sign in with an Admin account to access the dashboard.";
            return RedirectToAction("Login", "Auth");
        }

        // Số liệu thật (best-effort: nguồn nào lỗi thì card hiện "—").
        var model = await dashboardService.GetStatsAsync(HttpContext.RequestAborted);
        return View(model);
    }

    // AJAX same-origin cho chart doanh thu theo khoảng ngày (from/to yyyy-MM-dd).
    [HttpGet]
    public async Task<IActionResult> RevenueSeries(DateTime? from, DateTime? to)
    {
        if (!IsAdminRole()) return Unauthorized(new { success = false, message = "Admin only." });

        var nowVn = DateTime.UtcNow.AddHours(7).Date;
        var f = (from ?? new DateTime(nowVn.Year, nowVn.Month, 1)).Date;
        var t = (to ?? nowVn).Date;

        var result = await dashboardService.GetRevenueDailyAsync(f, t, HttpContext.RequestAborted);
        if (result is null)
            return StatusCode(502, new { success = false, message = "Cannot reach PaymentAPI. Make sure the gateway and PaymentAPI are running." });

        return Json(new { success = true, data = result });
    }

    // Trang quản lý AI / Interview Feedback. Guard UI: chỉ Admin/Manager; backend cũng tự chặn quyền.
    public IActionResult AiFeedback()
    {
        if (!IsAdminRole())
        {
            TempData["Error"] = "You must sign in with an Admin account to access the dashboard.";
            return RedirectToAction("Login", "Auth");
        }

        return View();
    }

    // Trang quản lý System Feedback (list + reply). Guard UI: chỉ Admin/Manager; backend tự chặn quyền.
    public IActionResult SystemFeedback()
    {
        if (!IsAdminRole())
        {
            TempData["Error"] = "You must sign in with an Admin account to access the dashboard.";
            return RedirectToAction("Login", "Auth");
        }

        return View();
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
        if (!IsAdminRole())
        {
            TempData["Error"] = "You must sign in with an Admin account to access the dashboard.";
            return RedirectToAction("Login", "Auth");
        }

        var model = await BuildReportedFeedbackModelAsync(
            page,
            feedbackType,
            status,
            reasonCode,
            keyword,
            reportId,
            cancellationToken);
        model.IsAdmin = true;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DismissFeedbackReport(
        Guid reportId,
        ResolveFeedbackReportRequest request,
        int page = 1,
        string feedbackType = "All",
        string status = FeedbackReportRules.PendingStatus,
        string reasonCode = "All",
        string keyword = "",
        CancellationToken cancellationToken = default) =>
        ResolveReportedFeedbackAsync(
            () => feedbackService.DismissFeedbackReportAsync(reportId, request, cancellationToken),
            "The report was dismissed.",
            page,
            feedbackType,
            status,
            reasonCode,
            keyword);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteReportedFeedback(
        Guid reportId,
        ResolveFeedbackReportRequest request,
        int page = 1,
        string feedbackType = "All",
        string status = FeedbackReportRules.PendingStatus,
        string reasonCode = "All",
        string keyword = "",
        CancellationToken cancellationToken = default) =>
        ResolveReportedFeedbackAsync(
            () => feedbackService.DeleteReportedFeedbackAsync(reportId, request, cancellationToken),
            "The feedback was removed from public view and its pending reports were resolved.",
            page,
            feedbackType,
            status,
            reasonCode,
            keyword);

    private async Task<IActionResult> ResolveReportedFeedbackAsync(
        Func<Task<WebClient.Models.Shared.ApiCallResult<FeedbackReportResolutionDto>>> action,
        string successMessage,
        int page,
        string feedbackType,
        string status,
        string reasonCode,
        string keyword)
    {
        if (!IsAdminRole())
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!ModelState.IsValid)
        {
            TempData["ReportedFeedbackError"] = "Resolution note cannot exceed 1000 characters.";
            return RedirectToReportedFeedback(page, feedbackType, status, reasonCode, keyword);
        }

        var result = await action();
        if (result.Success)
        {
            TempData["ReportedFeedbackSuccess"] = successMessage;
        }
        else
        {
            TempData["ReportedFeedbackError"] = ReportActionMessage(result.StatusCode, result.Error);
        }

        return RedirectToReportedFeedback(page, feedbackType, status, reasonCode, keyword);
    }

    private async Task<ReportedFeedbackPageViewModel> BuildReportedFeedbackModelAsync(
        int page,
        string feedbackType,
        string status,
        string reasonCode,
        string keyword,
        Guid? reportId,
        CancellationToken cancellationToken)
    {
        const int pageSize = 20;
        var list = await feedbackService.GetFeedbackReportsAsync(
            manager: false,
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
            ErrorMessage = list.Success ? TempData["ReportedFeedbackError"] as string : list.Error,
            SuccessMessage = TempData["ReportedFeedbackSuccess"] as string
        };

        if (reportId.HasValue)
        {
            var detail = await feedbackService.GetFeedbackReportDetailsAsync(
                manager: false,
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

        return model;
    }

    private IActionResult RedirectToReportedFeedback(
        int page,
        string feedbackType,
        string status,
        string reasonCode,
        string keyword) =>
        RedirectToAction(nameof(ReportedFeedback), new
        {
            page,
            feedbackType,
            status,
            reasonCode,
            keyword
        });

    private static string ReportActionMessage(int statusCode, string? error) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => error ?? "Please correct the resolution fields.",
            StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden => "You do not have permission to resolve feedback reports.",
            StatusCodes.Status404NotFound => "The report or feedback no longer exists.",
            StatusCodes.Status409Conflict => error ?? "This report is no longer pending.",
            StatusCodes.Status429TooManyRequests => "Too many requests. Please wait and try again.",
            _ => "The report could not be updated. Please try again."
        };

    // Trang quản lý Interview Sessions (list + detail transcript). Guard UI: Admin/Manager; backend tự chặn.
    public IActionResult InterviewSessions()
    {
        if (!IsAdminRole())
        {
            TempData["Error"] = "You must sign in with an Admin account to access the dashboard.";
            return RedirectToAction("Login", "Auth");
        }

        return View();
    }

    /// <summary>
    /// Đọc role từ access_token (JWT trong cookie HttpOnly) và kiểm tra hết hạn.
    /// CHỈ cho phép role Admin (Manager KHÔNG được vào khu Admin).
    /// Đây là cổng chặn ở tầng UI; các API admin phía backend vẫn tự kiểm quyền theo từng endpoint.
    /// </summary>
    private bool IsAdminRole()
    {
        var token = Request.Cookies["access_token"];
        if (string.IsNullOrWhiteSpace(token)) return false;

        var parts = token.Split('.');
        if (parts.Length < 2) return false;

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Token hết hạn -> coi như chưa đăng nhập.
            if (root.TryGetProperty("exp", out var expEl) && expEl.TryGetInt64(out var exp)
                && DateTimeOffset.FromUnixTimeSeconds(exp) < DateTimeOffset.UtcNow)
            {
                return false;
            }

            string? role = null;
            if (root.TryGetProperty(RoleClaim, out var r)) role = r.GetString();
            else if (root.TryGetProperty("role", out var r2)) role = r2.GetString();

            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

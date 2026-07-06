using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Admin;

namespace WebClient.Controllers.Admin;

public class AdminController(IAdminService adminService) : Controller
{
    // Claim role trong JWT do AuthAPI phát (chuẩn ClaimTypes.Role).
    private const string RoleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    public IActionResult Index()
    {
        // Chỉ tài khoản Admin mới vào được khu Admin (Manager KHÔNG được vào).
        if (!IsAdminRole())
        {
            TempData["Error"] = "You must sign in with an Admin account to access the dashboard.";
            return RedirectToAction("Login", "Auth");
        }

        return View();
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

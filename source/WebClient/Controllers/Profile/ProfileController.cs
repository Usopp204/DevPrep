using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Profile;
using WebClient.Services.Interview;
using WebClient.Services.Payment;
using WebClient.Services.Profile;
using WebClient.Services.Shared;

namespace WebClient.Controllers.Profile;

public class ProfileController(
    IProfileService profileService,
    IPaymentService paymentService,
    IBiometricsService biometricsService,
    IInterviewService interviewService) : Controller
{
    // ============================ View Profile ============================
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await profileService.GetMyProfileAsync();
        if (result.Unauthorized)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (!result.Success || result.Profile is null)
        {
            TempData["Error"] = result.Message;
            return View(new ProfileViewModel());
        }

        return View(result.Profile);
    }

    // ============================ Edit Profile ============================
    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var result = await profileService.GetMyProfileAsync();
        if (result.Unauthorized)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (!result.Success || result.Profile is null)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        return View(new EditProfileViewModel
        {
            FullName = result.Profile.FullName ?? string.Empty,
            Email = result.Profile.Email,
            AvatarUrl = result.Profile.AvatarUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await profileService.UpdateMyProfileAsync(model);
        if (result.Unauthorized)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (result.Success)
        {
            TempData["Message"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, result.Message);
        return View(model);
    }

    // ============================ Change Password ============================
    [HttpGet]
    public IActionResult ChangePassword()
    {
        if (string.IsNullOrEmpty(Request.Cookies["access_token"]))
        {
            return RedirectToAction("Login", "Auth");
        }

        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await profileService.ChangePasswordAsync(model);
        if (result.Unauthorized)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (result.Success)
        {
            TempData["Message"] = result.Message;
            return RedirectToAction(nameof(ChangePassword));
        }

        ModelState.AddModelError(string.Empty, result.Message);
        return View(model);
    }

    // ============================ CV & JD ============================
    [HttpGet]
    public IActionResult CvJd()
    {
        if (string.IsNullOrEmpty(Request.Cookies["access_token"]))
            return RedirectToAction("Login", "Auth");

        ViewData["ProfileActive"] = "cvjd";
        return View();
    }

    // ============================ Identity Verification (biometrics) ============================
    [HttpGet]
    public async Task<IActionResult> Identity(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(Request.Cookies["access_token"]))
            return RedirectToAction("Login", "Auth");

        ViewData["ProfileActive"] = "identity";
        var (success, status, error) = await biometricsService.GetStatusAsync(cancellationToken);

        // Recent sessions feed the temporary "Test verification" section (Milestone 2);
        // best-effort — the enrollment part of the page works without them.
        var recentSessions = new List<WebClient.Models.Interview.SessionListItemDto>();
        try
        {
            var dashboard = await interviewService.GetDashboardAsync(cancellationToken);
            recentSessions = dashboard.RecentSessions;
        }
        catch
        {
            // Ignore: the test section simply shows no sessions.
        }

        return View(new BiometricsViewModel
        {
            Status = status ?? new BiometricStatusModel(),
            ErrorMessage = success ? null : error,
            RecentSessions = recentSessions
        });
    }

    // Same-origin proxy for face enrollment: forwards the captured snapshot to the Gateway
    // (InterviewAPI) so the browser never needs the bearer token. Mirrors Interview/Stt.
    [HttpPost]
    public async Task<IActionResult> EnrollFace(IFormFile face, CancellationToken cancellationToken)
    {
        if (face is null || face.Length == 0)
        {
            return BadRequest(new { error = "A face image is required." });
        }

        using var stream = new MemoryStream();
        await face.CopyToAsync(stream, cancellationToken);
        var contentType = string.IsNullOrEmpty(face.ContentType) ? "image/jpeg" : face.ContentType;
        var fileName = string.IsNullOrEmpty(face.FileName) ? "face.jpg" : face.FileName;

        var (success, error) = await biometricsService.EnrollAsync(
            "face", stream.ToArray(), fileName, contentType, cancellationToken);

        if (!success)
        {
            return BadRequest(new { error = error ?? "Could not enroll face." });
        }

        return Json(new { enrolled = true });
    }

    // Same-origin proxy for voice enrollment (recorded sample as multipart, like Interview/Stt).
    [HttpPost]
    public async Task<IActionResult> EnrollVoice(IFormFile voice, CancellationToken cancellationToken)
    {
        if (voice is null || voice.Length == 0)
        {
            return BadRequest(new { error = "A voice recording is required." });
        }

        using var stream = new MemoryStream();
        await voice.CopyToAsync(stream, cancellationToken);
        var contentType = string.IsNullOrEmpty(voice.ContentType) ? "audio/webm" : voice.ContentType;
        var fileName = string.IsNullOrEmpty(voice.FileName) ? "voice.webm" : voice.FileName;

        var (success, error) = await biometricsService.EnrollAsync(
            "voice", stream.ToArray(), fileName, contentType, cancellationToken);

        if (!success)
        {
            return BadRequest(new { error = error ?? "Could not enroll voice." });
        }

        return Json(new { enrolled = true });
    }

    // ============================ Transaction History ============================
    public async Task<IActionResult> TransactionHistory(int page = 1, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(Request.Cookies["access_token"]))
            return RedirectToAction("Login", "Auth");

        var txRole = Request.Cookies["user_role"] ?? string.Empty;
        if (string.Equals(txRole, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(txRole, "Manager", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "Admin");

        var userId = JwtUserId.FromToken(Request.Cookies["access_token"]);
        if (userId is null)
            return RedirectToAction("Login", "Auth");

        var model = await paymentService.GetTransactionHistoryAsync(userId.Value, page, ct: ct);
        return View(model);
    }

    // ============================ Credit History ============================
    // Lịch sử trừ/cộng credit (CreditLedger): Usage (trừ khi phỏng vấn) / Purchase / Refund.
    public async Task<IActionResult> CreditHistory(int page = 1, string? type = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(Request.Cookies["access_token"]))
            return RedirectToAction("Login", "Auth");

        var role = Request.Cookies["user_role"] ?? string.Empty;
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "Admin");

        var userId = JwtUserId.FromToken(Request.Cookies["access_token"]);
        if (userId is null)
            return RedirectToAction("Login", "Auth");

        var model = await paymentService.GetCreditHistoryAsync(userId.Value, page, type: type, ct: ct);
        return View(model);
    }

    // ============================ Credit Balance (JSON cho dropdown navbar) ============================
    // Dropdown avatar ở _Navbar fetch endpoint này để hiện số credit hiện có (same-origin, khỏi CORS).
    [HttpGet]
    public async Task<IActionResult> CreditBalance(CancellationToken ct = default)
    {
        var userId = JwtUserId.FromToken(Request.Cookies["access_token"]);
        if (userId is null) return Unauthorized();

        var balance = await paymentService.GetCreditBalanceAsync(userId.Value, ct);
        return Json(new { balance = balance.Balance });
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Auth;
using WebClient.Services.Auth;

namespace WebClient.Controllers.Auth;

public class AuthController(IAuthService authService) : Controller
{
    // ============================ Login ============================
    [HttpGet]
    public IActionResult Login()
    {
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authService.LoginAsync(model);
        if (result.Success && result.Data is not null)
        {
            await StoreAuthCookies(result.Data);
            return RedirectAfterLogin(result.Data.Role);
        }

        ModelState.AddModelError(string.Empty, result.Message);
        return View(model);
    }

    // ============================ SSO (Google/GitHub via Firebase) ============================
    // FE lấy Firebase ID token (popup Google/GitHub) rồi POST idToken lên đây.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SsoFirebase(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            TempData["Error"] = "SSO sign-in failed: missing token.";
            return RedirectToAction(nameof(Login));
        }

        var result = await authService.SsoFirebaseAsync(idToken);
        if (result.Success && result.Data is not null)
        {
            await StoreAuthCookies(result.Data);
            return RedirectAfterLogin(result.Data.Role);
        }

        TempData["Error"] = result.Message;
        return RedirectToAction(nameof(Login));
    }

    // ============================ Logout ============================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refresh_token"];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            // Thu hồi refresh token phía backend (best-effort).
            await authService.LogoutAsync(refreshToken);
        }

        foreach (var name in new[] { "access_token", "refresh_token", "user_email", "user_role" })
        {
            Response.Cookies.Delete(name);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction("Index", "Home");
    }

    // ============================ Register ============================
    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authService.RegisterAsync(model);
        if (result.Success)
        {
            TempData["Message"] = result.Message;
            return RedirectToAction(nameof(VerifyEmail), new { email = result.Email });
        }

        ModelState.AddModelError(string.Empty, result.Message);
        return View(model);
    }

    // ============================ Verify Email ============================
    [HttpGet]
    public IActionResult VerifyEmail(string? email)
    {
        return View(new VerifyEmailViewModel { Email = email ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authService.VerifyEmailAsync(model.Email, model.Otp);
        if (result.Success)
        {
            TempData["Message"] = "Your email has been verified. Please log in.";
            return RedirectToAction(nameof(Login));
        }

        ModelState.AddModelError(string.Empty, result.Message);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp(string email)
    {
        var result = await authService.ResendOtpAsync(email);
        TempData["Message"] = result.Message;
        return RedirectToAction(nameof(VerifyEmail), new { email });
    }

    // ============================ Forgot Password ============================
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authService.ForgotPasswordAsync(model.Email);
        // Backend luôn trả thông báo chung (chống dò email) -> hiện luôn.
        TempData["Message"] = result.Message;
        return RedirectToAction(nameof(ForgotPassword));
    }

    // ============================ Reset Password ============================
    [HttpGet]
    public IActionResult ResetPassword(string? token)
    {
        return View(new ResetPasswordViewModel { Token = token ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authService.ResetPasswordAsync(model);
        if (result.Success)
        {
            TempData["Message"] = "Your password has been reset. Please log in.";
            return RedirectToAction(nameof(Login));
        }

        ModelState.AddModelError(string.Empty, result.Message);
        return View(model);
    }

    // ============================ helpers ============================
    // Admin -> thẳng Admin Dashboard; còn lại -> Home.
    private IActionResult RedirectAfterLogin(string? role)
    {
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("Index", "Admin");
        }

        return RedirectToAction("Index", "Home");
    }

    private async Task StoreAuthCookies(LoginResponseData data)
    {
        var secureOpts = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };
        var readableOpts = new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };

        if (!string.IsNullOrEmpty(data.AccessToken))
            Response.Cookies.Append("access_token", data.AccessToken, secureOpts);
        if (!string.IsNullOrEmpty(data.RefreshToken))
            Response.Cookies.Append("refresh_token", data.RefreshToken, secureOpts);

        Response.Cookies.Append("user_email", data.Email ?? string.Empty, readableOpts);
        Response.Cookies.Append("user_role", data.Role ?? string.Empty, readableOpts);

        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(data.Email))
            claims.Add(new Claim(ClaimTypes.Email, data.Email));
        if (!string.IsNullOrEmpty(data.Role))
            claims.Add(new Claim(ClaimTypes.Role, data.Role));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }
}

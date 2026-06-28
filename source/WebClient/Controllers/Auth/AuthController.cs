using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Auth;

namespace WebClient.Controllers.Auth;

public class AuthController(IAuthService authService) : Controller
{
    public IActionResult Login()
    {
        return View(authService.GetPage("Login"));
    }

    public IActionResult Register()
    {
        return View(authService.GetPage("Register"));
    }

    public IActionResult VerifyEmail()
    {
        return View(authService.GetPage("Verify Email"));
    }

    public IActionResult ForgotPassword()
    {
        return View(authService.GetPage("Forgot Password"));
    }

    public IActionResult ResetPassword()
    {
        return View(authService.GetPage("Reset Password"));
    }
}

using Microsoft.AspNetCore.Mvc;

namespace WebClient.Controllers.Shared;

public static class CandidateAuthenticationRedirect
{
    public const string ExpiredMessage = "Your login session has expired. Please sign in again.";

    public static IActionResult ToLogin(Controller controller, string? returnUrl)
    {
        controller.TempData["Error"] = ExpiredMessage;
        return controller.RedirectToAction("Login", "Auth", new { returnUrl });
    }
}
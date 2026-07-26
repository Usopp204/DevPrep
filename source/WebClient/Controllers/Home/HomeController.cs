using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Feedback;
using WebClient.Models.Home;
using WebClient.Models.Shared;
using WebClient.Services.Feedback;
using WebClient.Services.Interview;

namespace WebClient.Controllers.Home;

public class HomeController(IInterviewService interviewService, IFeedbackService feedbackService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new HomeViewModel
        {
            SuccessMessage = TempData["SuccessMessage"] as string,
            ErrorMessage = TempData["ErrorMessage"] as string,
            FeedbackValidationMessage = TempData[FeedbackTempDataKeys.SystemValidation] as string
        };

        if (TempData[FeedbackTempDataKeys.SystemForm] is string serializedForm)
        {
            model.SystemFeedbackForm = JsonSerializer.Deserialize<SubmitSystemFeedbackRequest>(serializedForm) ?? new();
            model.FeedbackFormWasSubmitted = true;
        }

        if (IsAuthenticatedUser())
        {
            var eligibility = await interviewService.GetFeedbackEligibilityAsync(cancellationToken);
            var status = eligibility.Success
                ? await feedbackService.GetSystemSubmissionStatusAsync(cancellationToken)
                : ApiCallResult<WebClient.Models.Feedback.SystemFeedbackSubmissionStatusDto>.Fail("Status unavailable.", StatusCodes.Status503ServiceUnavailable);

            model.ShowSystemFeedbackPopup = eligibility.Success
                && status.Success
                && eligibility.Data?.QualifyingSessionCount >= 1
                && status.Data is not null
                && (!status.Data.HasSubmittedSystemFeedback || status.Data.FeedbackCount < 1);
        }

        return View(model);
    }

    public IActionResult Error()
    {
        return View("~/Views/Shared/Error.cshtml", new ModulePageViewModel
        {
            ModuleName = "Error",
            Epic = "Shared",
            Summary = "The request could not be completed."
        });
    }

    private bool IsAuthenticatedUser()
    {
        var token = Request.Cookies["access_token"];
        var role = Request.Cookies["user_role"];
        return !string.IsNullOrEmpty(token) && string.Equals(role, "User", StringComparison.OrdinalIgnoreCase);
    }
}
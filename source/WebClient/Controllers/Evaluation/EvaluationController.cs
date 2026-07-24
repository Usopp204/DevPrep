using System.Text.Json;
using WebClient.Models.Feedback;
using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Evaluation;
using WebClient.Services.Feedback;
using WebClient.Services.Interview;

namespace WebClient.Controllers.Evaluation;

public class EvaluationController(
    IEvaluationService evaluationService,
    IFeedbackService feedbackService,
    IInterviewService interviewService) : Controller
{
    public IActionResult Index()
    {
        return View(evaluationService.GetOverview());
    }

    public async Task<IActionResult> Report(Guid sessionId, int durationActualSec, CancellationToken cancellationToken)
    {
        var (success, report, error) = await evaluationService.GetReportAsync(sessionId, durationActualSec, cancellationToken);

        if (!success || report is null)
        {
            TempData["ErrorMessage"] = error ?? "Could not load the report.";
            return RedirectToAction("Index", "Interview");
        }

        report.FeedbackForm.SessionId = sessionId;
        report.FeedbackMessage = TempData["FeedbackMessage"] as string;
        report.FeedbackValidationMessage = TempData[FeedbackTempDataKeys.InterviewValidation] as string;
        if (TempData[FeedbackTempDataKeys.InterviewForm] is string serializedForm)
        {
            report.FeedbackForm = JsonSerializer.Deserialize<SubmitInterviewFeedbackRequest>(serializedForm) ?? new();
            report.FeedbackForm.SessionId = sessionId;
            report.FeedbackFormWasSubmitted = true;
        }

        var eligibility = await interviewService.GetSessionFeedbackEligibilityAsync(sessionId, cancellationToken);
        report.CanSubmitInterviewFeedback = string.Equals(report.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            && report.Report is not null
            && eligibility.Success
            && eligibility.Data?.Eligible == true;

        var submittedFeedback = await feedbackService.GetMyInterviewFeedbackAsync(sessionId, cancellationToken);
        if (submittedFeedback.Success)
        {
            report.MyInterviewFeedback = submittedFeedback.Data;
            report.CanSubmitInterviewFeedback = false;
        }
        else if (submittedFeedback.StatusCode != StatusCodes.Status404NotFound)
        {
            report.FeedbackMessage ??= "Could not load your interview feedback right now.";
            report.CanSubmitInterviewFeedback = false;
        }

        return View(report);
    }
}

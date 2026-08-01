using System.Text.Json;
using WebClient.Models.Feedback;
using WebClient.Controllers.Shared;
using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Evaluation;
using WebClient.Services.Feedback;
using WebClient.Services.Interview;
using WebClient.Services.Shared;

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
        (bool success, WebClient.Models.Evaluation.ReportViewModel? report, string? error) result;
        try
        {
            result = await evaluationService.GetReportAsync(sessionId, durationActualSec, cancellationToken);
        }
        catch (AuthenticationExpiredException)
        {
            return CandidateAuthenticationRedirect.ToLogin(this, Request.Path + Request.QueryString);
        }

        var (success, report, error) = result;
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
        if (TempData[FeedbackTempDataKeys.InterviewEditForm] is string serializedEditForm)
        {
            report.EditFeedbackForm = JsonSerializer.Deserialize<UpdateInterviewFeedbackRequest>(serializedEditForm) ?? new();
            report.EditingFeedbackId = TempData[FeedbackTempDataKeys.InterviewEditFeedbackId] is string feedbackIdText
                && Guid.TryParse(feedbackIdText, out var feedbackId)
                    ? feedbackId
                    : null;
            report.FeedbackEditFormWasSubmitted = true;
        }

        var eligibility = await interviewService.GetSessionFeedbackEligibilityAsync(sessionId, cancellationToken);
        if (eligibility.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return CandidateAuthenticationRedirect.ToLogin(this, Request.Path + Request.QueryString);
        }

        report.CanSubmitInterviewFeedback = eligibility.Success
            && eligibility.Data?.CanSubmitInterviewFeedback == true;

        var submittedFeedback = await feedbackService.GetMyInterviewFeedbackAsync(sessionId, cancellationToken);
        if (submittedFeedback.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return CandidateAuthenticationRedirect.ToLogin(this, Request.Path + Request.QueryString);
        }

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

using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Evaluation;

namespace WebClient.Controllers.Evaluation;

public class EvaluationController(IEvaluationService evaluationService) : Controller
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

        return View(report);
    }
}

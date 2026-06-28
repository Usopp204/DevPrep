using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Evaluation;

namespace WebClient.Controllers.Evaluation;

public class EvaluationController(IEvaluationService evaluationService) : Controller
{
    public IActionResult Index()
    {
        return View(evaluationService.GetOverview());
    }
}

using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Interview;

namespace WebClient.Controllers.Interview;

public class InterviewController(IInterviewService interviewService) : Controller
{
    public IActionResult Index()
    {
        return View(interviewService.GetOverview());
    }
}

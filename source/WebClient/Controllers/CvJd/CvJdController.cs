using Microsoft.AspNetCore.Mvc;
using WebClient.Services.CvJd;

namespace WebClient.Controllers.CvJd;

public class CvJdController(ICvJdService cvJdService) : Controller
{
    public IActionResult Index()
    {
        return View(cvJdService.GetOverview());
    }
}

using Microsoft.AspNetCore.Mvc;
using WebClient.Services.News;

namespace WebClient.Controllers.News;

public class NewsController(INewsService newsService) : Controller
{
    public IActionResult Index()
    {
        return View(newsService.GetOverview());
    }
}

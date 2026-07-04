using Microsoft.AspNetCore.Mvc;
using WebClient.Services.News;

namespace WebClient.Controllers.News;

// Public: KHÔNG [Authorize] -> cả guest lẫn user đăng nhập đều xem được.
public class NewsController(INewsService newsService) : Controller
{
    private const int PageSize = 9;

    public async Task<IActionResult> Index(int page = 1)
    {
        var model = await newsService.GetListAsync(page, PageSize);
        return View(model);
    }
}

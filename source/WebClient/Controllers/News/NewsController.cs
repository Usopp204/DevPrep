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

    // /News/Detail/{id} — public. Backend tự tăng ViewCount mỗi lần xem.
    public async Task<IActionResult> Detail(Guid id)
    {
        var model = await newsService.GetDetailAsync(id);
        return View(model);
    }
}

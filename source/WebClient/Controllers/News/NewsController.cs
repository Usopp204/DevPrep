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

    // /News/Detail/{slug} — public, URL dùng slug (SEO). Backend tự tăng ViewCount mỗi lần xem.
    // Tương thích ngược: link cũ /News/Detail/{guid} vẫn vào được -> tự chuyển hướng sang URL slug.
    [HttpGet("/News/Detail/{slug}")]
    public async Task<IActionResult> Detail(string slug)
    {
        if (Guid.TryParse(slug, out var id))
        {
            var byId = await newsService.GetDetailAsync(id);
            if (byId.Found && !string.IsNullOrWhiteSpace(byId.Slug))
                return RedirectToAction(nameof(Detail), new { slug = byId.Slug });
            return View(byId);
        }

        var model = await newsService.GetDetailBySlugAsync(slug);
        return View(model);
    }
}

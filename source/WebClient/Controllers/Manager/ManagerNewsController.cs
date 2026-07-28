using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Manager;

namespace WebClient.Controllers.Manager;

[Authorize(Roles = "Manager")]
[Route("Manager/News")]
public class ManagerNewsController : Controller
{
    private readonly IManagerNewsService _service;

    public ManagerNewsController(IManagerNewsService service)
    {
        _service = service;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] int page = 1,
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? categoryId = null)
    {
        var result = await _service.GetListAsync(page, 10, keyword, status, categoryId);
        var categories = await _service.GetCategoriesAsync();

        ViewBag.Keyword = keyword;
        ViewBag.Status = status;
        ViewBag.CategoryId = categoryId;
        ViewBag.Categories = categories;

        return View("~/Views/ManagerNews/Index.cshtml", result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var model = await _service.GetByIdAsync(id);
        if (model is null) return NotFound();

        return View("~/Views/ManagerNews/Details.cshtml", model);
    }
}

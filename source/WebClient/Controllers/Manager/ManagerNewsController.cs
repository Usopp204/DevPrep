using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Manager;

namespace WebClient.Controllers.Manager;

[Authorize(Roles = "Manager,Admin")]
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
        [FromQuery] string? status = null)
    {
        var result = await _service.GetListAsync(page, 10, keyword, status);

        ViewBag.Keyword = keyword;
        ViewBag.Status = status;

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

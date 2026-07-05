using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Manager;

namespace WebClient.Controllers.Manager;

[Authorize(Roles = "Manager")]
[Route("Manager/Users")]
public class ManagerUserController : Controller
{
    private readonly IManagerUserService _service;

    public ManagerUserController(IManagerUserService service)
    {
        _service = service;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] int page = 1,
        [FromQuery] string? keyword = null)
    {
        Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        Response.Headers.Append("Pragma", "no-cache");
        Response.Headers.Append("Expires", "0");

        var pagedResult = await _service.GetListAsync(page, 10, keyword);

        ViewBag.Keyword = keyword;

        return View("~/Views/ManagerUser/Index.cshtml", pagedResult);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var model = await _service.GetByIdAsync(id);
        if (model == null)
            return NotFound();

        return PartialView(model);
    }
}

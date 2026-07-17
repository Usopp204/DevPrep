using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Manager;

namespace WebClient.Controllers.Manager;

[Authorize(Roles = "Manager")]
[Route("Manager/QuestionBank")]
public class ManagerQuestionBankController : Controller
{
    private readonly IManagerQuestionService _service;

    public ManagerQuestionBankController(IManagerQuestionService service)
    {
        _service = service;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] int page = 1,
        [FromQuery] string? keyword = null,
        [FromQuery] string? difficulty = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? categoryId = null)
    {
        var listTask = _service.GetListAsync(page, 10, keyword, null, null, difficulty, status, categoryId);
        var categoriesTask = _service.GetCategoriesAsync();
        await Task.WhenAll(listTask, categoriesTask);
        var pagedResult = await listTask;
        
        // Pass filter values back to the view
        ViewBag.Keyword = keyword;
        ViewBag.Difficulty = difficulty;
        ViewBag.Status = status;
        ViewBag.CategoryId = categoryId;
        ViewBag.Categories = await categoriesTask;
        
        return View("~/Views/ManagerQuestionBank/Index.cshtml", pagedResult);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var model = await _service.GetByIdAsync(id);
        if (model == null)
            return NotFound();
            
        return View("~/Views/ManagerQuestionBank/Details.cshtml", model);
    }
}

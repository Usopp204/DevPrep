using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Admin;
using WebClient.Services.Admin;

namespace WebClient.Controllers.Admin;

// Vai tro doc tu claim trong cookie xac thuc da ky, khong doc tu cookie
// user_role: cookie do duoc dat voi HttpOnly = false nen trinh duyet sua duoc.
[Authorize(Roles = "Admin")]
[Route("admin/questions")]
public class AdminQuestionController : Controller
{
    private readonly IAdminQuestionService _questionService;

    public AdminQuestionController(IAdminQuestionService questionService)
    {
        _questionService = questionService;
    }


    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AdminQuestionQueryDto query)
    {
        if (query.PageIndex <= 0)
            query.PageIndex = 1;

        if (query.PageSize <= 0)
            query.PageSize = 10;

        var viewModel = new AdminQuestionViewModel();

        ViewBag.CurrentQuery = query;
        ViewBag.CurrentCategory = query.CategoryId?.ToString() ?? "";
        ViewBag.CurrentStatus = query.Status ?? "";

        var apiResponse = await _questionService.GetQuestionsAsync(query);

        if (apiResponse.Success && apiResponse.Data != null)
        {
            viewModel.PagedResult = apiResponse.Data;
        }
        else
        {
            ViewBag.ErrorMessage = apiResponse.Message ?? "The question list could not be loaded.";
        }

        var categoryResponse = await _questionService.GetCategoriesAsync();

        if (categoryResponse.Success && categoryResponse.Data != null)
        {
            viewModel.Categories = categoryResponse.Data;
        }

        return View("~/Views/Admin/ViewListQuestion.cshtml", viewModel);
    }

    [HttpGet("detail/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var question = await _questionService.GetQuestionDetailAsync(id);

        if (question == null)
        {
            TempData["ErrorMessage"] = "The requested question was not found.";
            return Redirect("/admin/questions");
        }

        return View("~/Views/Admin/ViewQuestionDetail.cshtml", question);
    }

    //[HttpGet("create")]
    //public async Task<IActionResult> Create()
    //{
    //    var viewModel = new AdminQuestionViewModel();

    //    var categoryResponse = await _questionService.GetCategoriesAsync();

    //    if (categoryResponse.Success && categoryResponse.Data != null)
    //    {
    //        viewModel.Categories = categoryResponse.Data;
    //    }

    //    return View("~/Views/Admin/AddQuestion.cshtml", viewModel);
    //}

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        var viewModel = new AdminQuestionViewModel();

        var categoryResponse = await _questionService.GetCategoriesAsync();

        if (categoryResponse.Success && categoryResponse.Data != null)
        {
            viewModel.Categories = categoryResponse.Data;
        }

        var positionResponse = await _questionService.GetPositionsAsync();

        if (positionResponse.Success && positionResponse.Data != null)
        {
            viewModel.Positions = positionResponse.Data;
        }

        return View("~/Views/Admin/AddQuestion.cshtml", viewModel);
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromForm] AdminCreateQuestionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Question))
        {
            return BadRequest(new
            {
                success = false,
                message = "Please enter the question text."
            });
        }

        var result = await _questionService.CreateQuestionAsync(dto);

        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                message = result.Message ?? "Question added successfully."
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result.Message ?? "The question could not be created."
        });
    }

    [HttpGet("edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var question = await _questionService.GetQuestionDetailAsync(id);

        if (question == null)
        {
            TempData["ErrorMessage"] = "The question you want to edit was not found.";
            return Redirect("/admin/questions");
        }

        if (question.IsDeleted)
        {
            TempData["ErrorMessage"] = "This question has been deleted. Restore it before editing.";
            return Redirect("/admin/questions");
        }
        var viewModel = new AdminQuestionViewModel();

        var categoryResponse = await _questionService.GetCategoriesAsync();

        if (categoryResponse.Success && categoryResponse.Data != null)
        {
            viewModel.Categories = categoryResponse.Data;
        }
        else
        {
            ViewBag.CategoryError = categoryResponse.Message ?? "The category list could not be loaded.";
        }

        var positionResponse = await _questionService.GetPositionsAsync();

        if (positionResponse.Success && positionResponse.Data != null)
        {
            viewModel.Positions = positionResponse.Data;
        }

        ViewBag.QuestionDetail = question;

        return View("~/Views/Admin/UpdateQuestion.cshtml", viewModel);
    }

    [HttpPost("edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromForm] AdminUpdateQuestionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Question))
        {
            return BadRequest(new
            {
                success = false,
                message = "Please enter the question text."
            });
        }

        var result = await _questionService.UpdateQuestionAsync(id, dto);

        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                message = result.Message ?? "Question updated successfully."
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result.Message ?? "The question could not be updated."
        });
    }

    [HttpPost("delete/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _questionService.DeleteQuestionAsync(id);

        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                message = result.Message ?? "Question deleted successfully."
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result.Message ?? "The question could not be deleted."
        });
    }

    [HttpPost("restore/{id:guid}")]
    public async Task<IActionResult> Restore(Guid id)
    {
        var result = await _questionService.RestoreQuestionAsync(id);

        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                message = result.Message ?? "Question restored successfully."
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result.Message ?? "The question could not be restored."
        });
    }

    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] List<Guid> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Select at least one question to delete."
            });
        }

        var result = await _questionService.BulkDeleteQuestionsAsync(ids);

        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                message = result.Message ?? "The selected questions were deleted."
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result.Message ?? "The selected questions could not be deleted."
        });
    }
}
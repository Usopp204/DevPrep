using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebClient.Models.Admin;
using WebClient.Services.Admin;

namespace WebClient.Controllers.Admin;

[Route("admin/questions")]
public class AdminQuestionController : Controller
{
    private readonly IAdminQuestionService _questionService;

    public AdminQuestionController(IAdminQuestionService questionService)
    {
        _questionService = questionService;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (IsAdmin(context))
        {
            base.OnActionExecuting(context);
            return;
        }

        var request = context.HttpContext.Request;

        var isAjax =
            string.Equals(
                request.Headers["X-Requested-With"].ToString(),
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase
            );

        if (isAjax || request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                success = false,
                message = "Bạn không có quyền thực hiện thao tác này."
            });

            return;
        }

        context.Result = Redirect("/Auth/Login");
    }

    private static bool IsAdmin(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;

        var role = request.Cookies["user_role"];
        var accessToken = request.Cookies["access_token"];
        var jwtToken = request.Cookies["JWToken"];

        var hasToken =
            !string.IsNullOrWhiteSpace(accessToken) ||
            !string.IsNullOrWhiteSpace(jwtToken);

        return hasToken &&
               string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
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
            ViewBag.ErrorMessage = apiResponse.Message ?? "Không thể tải danh sách câu hỏi từ API.";
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
            TempData["ErrorMessage"] = "Không tìm thấy câu hỏi yêu cầu.";
            return Redirect("/admin/questions");
        }

        return View("~/Views/Admin/ViewQuestionDetail.cshtml", question);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        var viewModel = new AdminQuestionViewModel();

        var categoryResponse = await _questionService.GetCategoriesAsync();

        if (categoryResponse.Success && categoryResponse.Data != null)
        {
            viewModel.Categories = categoryResponse.Data;
        }

        return View("~/Views/Admin/AddQuestion.cshtml", viewModel);
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromForm] AdminCreateQuestionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
        {
            return BadRequest(new
            {
                success = false,
                message = "Vui lòng nhập nội dung câu hỏi."
            });
        }

        var result = await _questionService.CreateQuestionAsync(dto);

        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                message = result.Message ?? "Thêm câu hỏi thành công!"
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result.Message ?? "Không thể tạo câu hỏi qua API."
        });
    }

    [HttpGet("edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var question = await _questionService.GetQuestionDetailAsync(id);

        if (question == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy câu hỏi yêu cầu để chỉnh sửa.";
            return Redirect("/admin/questions");
        }

        if (question.IsDeleted)
        {
            TempData["ErrorMessage"] = "Câu hỏi này đã bị xóa mềm. Vui lòng khôi phục trước khi chỉnh sửa.";
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
            ViewBag.CategoryError = categoryResponse.Message ?? "Lỗi không xác định khi lấy danh mục.";
        }

        ViewBag.QuestionDetail = question;

        return View("~/Views/Admin/UpdateQuestion.cshtml", viewModel);
    }

    [HttpPost("edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromForm] AdminUpdateQuestionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
        {
            return BadRequest(new
            {
                success = false,
                message = "Vui lòng nhập nội dung câu hỏi."
            });
        }

        var result = await _questionService.UpdateQuestionAsync(id, dto);

        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                message = result.Message ?? "Cập nhật câu hỏi thành công!"
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result.Message ?? "Không thể cập nhật câu hỏi qua API."
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
                message = result.Message ?? "Xóa mềm câu hỏi thành công."
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result.Message ?? "Không thể thực hiện thao tác xóa."
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
                message = result.Message ?? "Khôi phục câu hỏi thành công."
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result.Message ?? "Không thể khôi phục câu hỏi."
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
                message = "Vui lòng chọn ít nhất một câu hỏi để xóa."
            });
        }

        var result = await _questionService.BulkDeleteQuestionsAsync(ids);

        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                message = result.Message ?? "Xóa hàng loạt câu hỏi thành công."
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result.Message ?? "Không thể xóa hàng loạt câu hỏi."
        });
    }
}
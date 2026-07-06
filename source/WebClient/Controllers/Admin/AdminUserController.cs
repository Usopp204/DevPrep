using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebClient.Models.Admin;
using WebClient.Services.Admin;

namespace WebClient.Controllers.Admin;

[Route("Admin/User")]
public class AdminUserController : Controller
{
    private readonly IAdminUserService _userService;

    public AdminUserController(IAdminUserService userService)
    {
        _userService = userService;
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
    public async Task<IActionResult> Index([FromQuery] AdminUserQueryDto query)
    {
        if (query.Page <= 0)
            query.Page = 1;

        if (query.PageSize <= 0)
            query.PageSize = 10;

        var viewModel = new AdminUserViewModel
        {
            Query = query
        };

        var response = await _userService.GetUsersAsync(query);

        if (response.Success && response.Data != null)
        {
            viewModel.PagedResult = response.Data;
        }
        else
        {
            ViewBag.ErrorMessage = response.Message ?? "Không thể tải danh sách người dùng.";
        }

        return View("~/Views/Admin/AdminListUser.cshtml", viewModel);
    }

    [HttpGet("Detail/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var response = await _userService.GetUserDetailAsync(id);

        if (!response.Success || response.Data == null)
        {
            TempData["ErrorMessage"] = response.Message ?? "Không tìm thấy người dùng.";
            return Redirect("/Admin/User");
        }

        return View("~/Views/Admin/AdminDetailUser.cshtml", response.Data);
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var response = await _userService.GetUserDetailAsync(id);

        if (!response.Success || response.Data == null)
        {
            TempData["ErrorMessage"] = response.Message ?? "Không tìm thấy người dùng để chỉnh sửa.";
            return Redirect("/Admin/User");
        }

        return View("~/Views/Admin/AdminEditUser.cshtml", response.Data);
    }

    [HttpPost("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromForm] AdminUpdateUserRoleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Role))
        {
            TempData["ErrorMessage"] = "Vui lòng chọn role hợp lệ.";
            return Redirect($"/Admin/User/Edit/{id}");
        }

        var result = await _userService.UpdateRoleAsync(id, dto);

        if (result.Success)
        {
            TempData["SuccessMessage"] = result.Message ?? "Cập nhật role thành công.";
            return Redirect($"/Admin/User/Detail/{id}");
        }

        TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật role.";
        return Redirect($"/Admin/User/Edit/{id}");
    }

    [HttpPost("status/{id:guid}")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] AdminUpdateUserStatusDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Status))
        {
            return BadRequest(new
            {
                success = false,
                message = "Trạng thái không hợp lệ."
            });
        }

        var result = await _userService.UpdateStatusAsync(id, dto);

        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                message = result.Message ?? "Cập nhật trạng thái người dùng thành công."
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result.Message ?? "Không thể cập nhật trạng thái người dùng."
        });
    }
}
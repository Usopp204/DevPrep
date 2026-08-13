using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Admin;
using WebClient.Services.Admin;

namespace WebClient.Controllers.Admin;

// Vai tro doc tu claim trong cookie xac thuc da ky, khong doc tu cookie
// user_role: cookie do duoc dat voi HttpOnly = false nen trinh duyet sua duoc.
[Authorize(Roles = "Admin")]
[Route("Admin/User")]
public class AdminUserController : Controller
{
    private readonly IAdminUserService _userService;

    public AdminUserController(IAdminUserService userService)
    {
        _userService = userService;
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
            ViewBag.ErrorMessage = response.Message ?? "The user list could not be loaded.";
        }

        return View("~/Views/Admin/AdminListUser.cshtml", viewModel);
    }

    [HttpGet("Detail/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var response = await _userService.GetUserDetailAsync(id);

        if (!response.Success || response.Data == null)
        {
            TempData["ErrorMessage"] = response.Message ?? "User not found.";
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
            TempData["ErrorMessage"] = response.Message ?? "The user you want to edit was not found.";
            return Redirect("/Admin/User");
        }

        return View("~/Views/Admin/AdminEditUser.cshtml", response.Data);
    }

    [HttpPost("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromForm] AdminUpdateUserRoleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Role))
        {
            TempData["ErrorMessage"] = "Choose a valid role.";
            return Redirect($"/Admin/User/Edit/{id}");
        }

        var result = await _userService.UpdateRoleAsync(id, dto);

        if (result.Success)
        {
            TempData["SuccessMessage"] = result.Message ?? "Role updated successfully.";
            return Redirect($"/Admin/User/Detail/{id}");
        }

        TempData["ErrorMessage"] = result.Message ?? "The role could not be updated.";
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
                message = "That status is not valid."
            });
        }

        var result = await _userService.UpdateStatusAsync(id, dto);

        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                message = result.Message ?? "User status updated successfully."
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result.Message ?? "The user status could not be updated."
        });
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Admin.TechStacks;
using WebClient.Services.Admin;

namespace WebClient.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/TechStacks")]
public sealed class AdminTechStacksController : Controller
{
    private const int PageSize = 6;
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "All", "Active", "Inactive"
    };

    private readonly IAdminTechStackService _techStackService;

    public AdminTechStacksController(IAdminTechStackService techStackService)
    {
        _techStackService = techStackService;
    }

    [HttpGet("")]
    public Task<IActionResult> Index(
        string? search,
        string? status,
        int pageIndex = 1,
        CancellationToken cancellationToken = default)
    {
        return LoadPageAsync(
            search,
            status,
            pageIndex,
            renderPartial: false,
            cancellationToken);
    }

    [HttpGet("Results")]
    public Task<IActionResult> Results(
        string? search,
        string? status,
        int pageIndex = 1,
        CancellationToken cancellationToken = default)
    {
        return LoadPageAsync(
            search,
            status,
            pageIndex,
            renderPartial: true,
            cancellationToken);
    }
    [HttpGet("Detail/{techStackId:guid}")]
    public async Task<IActionResult> Detail(Guid techStackId, CancellationToken cancellationToken)
    {
        var result = await _techStackService.GetByIdAsync(techStackId, cancellationToken);
        return ToActionResult(result, "Tech Stack loaded.");
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTechStackViewModel model, CancellationToken cancellationToken)
    {
        Normalize(model);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var result = await _techStackService.CreateAsync(model, cancellationToken);
        return ToActionResult(result, "Tech Stack created successfully.");
    }

    [HttpPost("Update/{techStackId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        Guid techStackId,
        UpdateTechStackViewModel model,
        CancellationToken cancellationToken)
    {
        Normalize(model);
        if (!model.IsActive.HasValue)
        {
            ModelState.AddModelError(nameof(model.IsActive), "Status is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var result = await _techStackService.UpdateAsync(techStackId, model, cancellationToken);
        return ToActionResult(result, "Tech Stack updated successfully.");
    }

    [HttpPost("Deactivate/{techStackId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid techStackId, CancellationToken cancellationToken)
    {
        var result = await _techStackService.DeactivateAsync(techStackId, cancellationToken);
        return ToActionResult(result, "Tech Stack deactivated successfully.");
    }

    [HttpPost("Reactivate/{techStackId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid techStackId, CancellationToken cancellationToken)
    {
        var result = await _techStackService.ReactivateAsync(techStackId, cancellationToken);
        return ToActionResult(result, "Tech Stack reactivated successfully.");
    }

    private async Task<IActionResult> LoadPageAsync(
        string? search,
        string? status,
        int pageIndex,
        bool renderPartial,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var normalizedStatus = NormalizeStatus(status);
        var normalizedPageIndex = Math.Max(1, pageIndex);
        var result = await _techStackService.GetListAsync(
            normalizedPageIndex,
            PageSize,
            normalizedSearch,
            normalizedStatus,
            cancellationToken);

        if (result.Status == AdminTechStackResultStatus.Unauthorized)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (result.Status == AdminTechStackResultStatus.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (result.IsSuccess && result.Data is not null)
        {
            var totalPages = Math.Max(0, result.Data.TotalPages);
            if (result.Data.TotalRecords > 0 &&
                totalPages > 0 &&
                normalizedPageIndex > totalPages)
            {
                return RedirectToAction(
                    renderPartial ? nameof(Results) : nameof(Index),
                    new
                    {
                        search = normalizedSearch,
                        status = normalizedStatus,
                        pageIndex = totalPages
                    });
            }

            var model = new AdminTechStackListViewModel
            {
                Items = result.Data.Items.Take(PageSize).ToList(),
                PageIndex = result.Data.TotalRecords == 0 ? 1 : normalizedPageIndex,
                PageSize = PageSize,
                TotalRecords = result.Data.TotalRecords,
                TotalPages = totalPages,
                Search = normalizedSearch,
                Status = normalizedStatus
            };

            return RenderList(model, renderPartial);
        }

        return RenderList(
            new AdminTechStackListViewModel
            {
                PageIndex = normalizedPageIndex,
                PageSize = PageSize,
                Search = normalizedSearch,
                Status = normalizedStatus,
                ApiErrorMessage = result.Message ??
                    "Tech Stack management is temporarily unavailable. Please try again."
            },
            renderPartial);
    }

    private IActionResult RenderList(AdminTechStackListViewModel model, bool renderPartial)
    {
        return renderPartial
            ? PartialView("_TechStackResults", model)
            : View("Index", model);
    }
    private IActionResult ToActionResult<T>(AdminTechStackServiceResult<T> result, string successMessage)
    {
        if (result.IsSuccess)
        {
            return Ok(new { success = true, message = successMessage, data = result.Data });
        }

        var body = new { success = false, message = result.Message ?? "The request could not be completed." };
        return result.Status switch
        {
            AdminTechStackResultStatus.ValidationFailure => BadRequest(body),
            AdminTechStackResultStatus.Unauthorized => Unauthorized(body),
            AdminTechStackResultStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, body),
            AdminTechStackResultStatus.NotFound => NotFound(body),
            AdminTechStackResultStatus.DuplicateConflict => Conflict(body),
            _ => StatusCode(StatusCodes.Status502BadGateway, body)
        };
    }

    private BadRequestObjectResult ValidationError()
    {
        var message = ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(error => !string.IsNullOrWhiteSpace(error))
            ?? "Please check the submitted Tech Stack data.";

        return BadRequest(new { success = false, message });
    }

    private void Normalize(CreateTechStackViewModel model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        if (model.Name.Length == 0)
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        }
    }

    private void Normalize(UpdateTechStackViewModel model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        if (model.Name.Length == 0)
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        }
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || !ValidStatuses.Contains(status.Trim()))
        {
            return "All";
        }

        return ValidStatuses.First(item => item.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

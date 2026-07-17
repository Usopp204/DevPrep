using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Manager.TechStacks;
using WebClient.Services.Manager;

namespace WebClient.Controllers.Manager;

[Authorize(Roles = "Manager")]
[Route("Manager/TechStacks")]
public sealed class ManagerTechStacksController : Controller
{
    private const int PageSize = 6;
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "All", "Active", "Inactive"
    };

    private readonly IManagerTechStackService _techStackService;

    public ManagerTechStacksController(IManagerTechStackService techStackService)
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

        if (result.Status == ManagerTechStackResultStatus.Unauthorized)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (result.Status == ManagerTechStackResultStatus.Forbidden)
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

            var model = new ManagerTechStackListViewModel
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
            new ManagerTechStackListViewModel
            {
                PageIndex = normalizedPageIndex,
                PageSize = PageSize,
                Search = normalizedSearch,
                Status = normalizedStatus,
                ApiErrorMessage = result.Message ??
                    "Tech Stacks are temporarily unavailable. Please try again."
            },
            renderPartial);
    }

    private IActionResult RenderList(
        ManagerTechStackListViewModel model,
        bool renderPartial)
    {
        return renderPartial
            ? PartialView("_TechStackResults", model)
            : View("Index", model);
    }

    private static string NormalizeStatus(string? status)
    {
        var candidate = status?.Trim();
        if (string.IsNullOrWhiteSpace(candidate) || !ValidStatuses.Contains(candidate))
        {
            return "All";
        }

        return ValidStatuses.First(validStatus =>
            validStatus.Equals(candidate, StringComparison.OrdinalIgnoreCase));
    }
}
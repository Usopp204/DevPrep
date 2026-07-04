using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentAPI.DTOs;
using PaymentAPI.Services;

namespace PaymentAPI.Controllers;

[ApiController]
[Route("api/admin/manager/credit-packages")]
[Authorize(Roles = "Manager,Admin")]
public class ManagerCreditPackagesController : ControllerBase
{
    private readonly ICreditPackageService _service;

    public ManagerCreditPackagesController(ICreditPackageService service)
    {
        _service = service;
    }

    /// <summary>
    /// View Credit Package List (Manager)
    /// GET /api/admin/manager/credit-packages
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CreditPackageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] bool? isActive = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var result = await _service.GetListAsync(page, pageSize, keyword, isActive);
        return Ok(ApiResponse<PagedResult<CreditPackageDto>>.Ok(result));
    }

    /// <summary>
    /// View Credit Package Details (Manager)
    /// GET /api/admin/manager/credit-packages/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CreditPackageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var item = await _service.GetByIdAsync(id);
        if (item is null)
            return NotFound(ApiResponse<CreditPackageDto>.Fail($"Credit package with id '{id}' not found."));

        return Ok(ApiResponse<CreditPackageDto>.Ok(item));
    }
}

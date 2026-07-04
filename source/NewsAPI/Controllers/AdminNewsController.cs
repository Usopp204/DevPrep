using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewsAPI.DTOs;
using NewsAPI.Models;
using NewsAPI.Services.Interfaces;
using System.Security.Claims;

namespace NewsAPI.Controllers
{
    [ApiController]
    [Route("api/admin/news")]
    [Authorize(Roles = "Admin")]
    public class AdminNewsController : ControllerBase
    {
        private readonly IAdminNewsService _service;

        public AdminNewsController(IAdminNewsService service)
        {
            _service = service;
        }

        // GET /api/admin/news
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] AdminNewsQueryDto q)
        {
            var result = await _service.GetListAsync(q);

            return Ok(ApiResponse<PagedResult<AdminNewsListItemDto>>.Ok(
                result,
                "Fetched news list successfully."
            ));
        }

        // GET /api/admin/news/categories
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _service.GetAllCategoriesAsync();

            return Ok(ApiResponse<List<NewsCategory>>.Ok(
                categories,
                "Fetched news categories successfully."
            ));
        }

        // GET /api/admin/news/filter
        [HttpGet("filter")]
        public async Task<IActionResult> Filter([FromQuery] AdminNewsQueryDto q)
        {
            var result = await _service.FilterAsync(q);

            return Ok(ApiResponse<PagedResult<AdminNewsListItemDto>>.Ok(
                result,
                "Filtered news successfully."
            ));
        }

        // GET /api/admin/news/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetDetail(Guid id)
        {
            var result = await _service.GetDetailAsync(id);

            if (result == null || result.IsDeleted)
            {
                return NotFound(ApiResponse<object>.Fail(
                    "Article not found or has been soft-deleted."
                ));
            }

            return Ok(ApiResponse<AdminNewsDetailDto>.Ok(
                result,
                "Fetched news details successfully."
            ));
        }

        // POST /api/admin/news
        [HttpPost]
        public async Task<IActionResult> Create(AdminCreateNewsDto dto)
        {
            try
            {
                var authorId = GetCurrentAdminId();
                var result = await _service.CreateAsync(dto, authorId);

                return Ok(ApiResponse<AdminNewsDetailDto>.Ok(
                    result,
                    "Created news article successfully."
                ));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // PUT /api/admin/news/{id}
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, AdminUpdateNewsDto dto)
        {
            try
            {
                var result = await _service.UpdateAsync(id, dto);

                if (result == null)
                {
                    return NotFound(ApiResponse<object>.Fail(
                        "Article not found to update."
                    ));
                }

                return Ok(ApiResponse<AdminNewsDetailDto>.Ok(
                    result,
                    "Updated news article successfully."
                ));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // DELETE /api/admin/news/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var adminId = GetCurrentAdminId();
            var success = await _service.DeleteAsync(id, adminId);

            if (!success)
            {
                return NotFound(ApiResponse<object>.Fail(
                    "Article not found or already soft-deleted."
                ));
            }

            return NoContent();
        }

        // POST /api/admin/news/{id}/restore
        [HttpPost("{id:guid}/restore")]
        public async Task<IActionResult> Restore(Guid id)
        {
            var success = await _service.RestoreAsync(id);

            if (!success)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    "Failed to restore. Article is not in soft-deleted state."
                ));
            }

            return Ok(ApiResponse<object>.Ok(
                true,
                "Reverted soft-deleted article back to Draft successfully."
            ));
        }

        private Guid GetCurrentAdminId()
        {
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? User.FindFirst("accountId")?.Value;

            return Guid.TryParse(claimId, out var parsedGuid)
                ? parsedGuid
                : Guid.Empty;
        }
    }
}

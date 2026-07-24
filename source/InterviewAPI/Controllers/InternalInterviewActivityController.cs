using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InterviewAPI.Models; // đổi lại namespace DbContext cho đúng project của bạn

namespace InterviewAPI.Controllers;

[ApiController]
[Route("api/internal/interviews")]
public class InternalInterviewActivityController : ControllerBase
{
    private readonly InterviewDbContext _context; // đổi tên DbContext cho đúng với project bạn

    public InternalInterviewActivityController(InterviewDbContext context)
    {
        _context = context;
    }

    // Lưu ý: cột UserId trong InterviewSession thực chất đang lưu AccountId
    // (đúng như comment trong code UserAPI: "InterviewAPI đang lưu AccountId vào cột UserId của nó")
    [HttpPost("last-activity")]
    public async Task<IActionResult> GetLastActivity([FromBody] List<Guid> accountIds)
    {
        if (accountIds == null || !accountIds.Any())
        {
            return Ok(new List<object>());
        }

        var result = await _context.InterviewSessions
            .Where(s => accountIds.Contains(s.UserId) && !s.IsDeleted)
            .GroupBy(s => s.UserId)
            .Select(g => new
            {
                AccountId = g.Key,
                LastActivityAt = g.Max(s => s.StartedAt ?? s.CreatedAt)
            })
            .ToListAsync();

        return Ok(result);
    }
}
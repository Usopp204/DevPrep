using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.Models; // đổi lại namespace DbContext cho đúng project

namespace PaymentAPI.Controllers;

[ApiController]
[Route("api/internal/payments")]
public class InternalPaymentActivityController : ControllerBase
{
    private readonly PaymentDbContext _context; // đổi tên DbContext cho đúng project

    public InternalPaymentActivityController(PaymentDbContext context)
    {
        _context = context;
    }

    [HttpPost("last-activity")]
    public async Task<IActionResult> GetLastActivity([FromBody] List<Guid> userIds)
    {
        if (userIds == null || !userIds.Any())
        {
            return Ok(new List<object>());
        }

        var result = await _context.Orders
            .Where(o => userIds.Contains(o.UserId) && o.Status == "Paid")
            .GroupBy(o => o.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                LastActivityAt = g.Max(o => o.PaidAt ?? o.CreatedAt)
            })
            .ToListAsync();

        return Ok(result);
    }
}
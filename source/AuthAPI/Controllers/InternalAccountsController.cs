using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuthAPI.Models;
using AuthAPI.Helpers;
using AuthAPI.DTOs; // Import namespace chứa file DTO gộp vừa tạo

namespace AuthAPI.Controllers;

[ApiController]
[Route("api/internal/accounts")]
public class InternalAccountsController : ControllerBase
{
    private readonly AuthDbContext _context;

    public InternalAccountsController(AuthDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // 1. LẤY DANH SÁCH ACCOUNTS
    // =========================================================
    [HttpGet("statistics/candidates")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetCandidateStatistics(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toExclusiveUtc,
        CancellationToken cancellationToken)
    {
        if (!fromUtc.HasValue || !toExclusiveUtc.HasValue || fromUtc.Value >= toExclusiveUtc.Value)
            return BadRequest(new { message = "A valid candidate statistics range is required." });

        var vietnamFrom = fromUtc.Value.AddHours(7);
        var vietnamToExclusive = toExclusiveUtc.Value.AddHours(7);
        if (vietnamFrom.TimeOfDay != TimeSpan.Zero || vietnamToExclusive.TimeOfDay != TimeSpan.Zero)
            return BadRequest(new { message = "Candidate statistics boundaries must be Vietnam calendar days." });

        var from = DateOnly.FromDateTime(vietnamFrom);
        var to = DateOnly.FromDateTime(vietnamToExclusive.AddDays(-1));
        var inclusiveDays = to.DayNumber - from.DayNumber + 1;
        if (inclusiveDays is < 1 or > 366)
            return BadRequest(new { message = "The selected date range must contain no more than 366 days." });

        var candidates = _context.Accounts.AsNoTracking()
            .Where(account => account.Role.ToUpper() == "USER");

        var totalCandidates = await candidates.LongCountAsync(cancellationToken);
        var grouped = await candidates
            .Where(account => account.CreatedAt >= fromUtc.Value && account.CreatedAt < toExclusiveUtc.Value)
            .GroupBy(account => account.CreatedAt.AddHours(7).Date)
            .Select(group => new { Date = group.Key, Count = group.LongCount() })
            .ToListAsync(cancellationToken);

        var byDate = grouped.ToDictionary(
            item => DateOnly.FromDateTime(item.Date),
            item => item.Count);

        var days = new List<CandidateStatisticsDayDto>(inclusiveDays);
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            days.Add(new CandidateStatisticsDayDto
            {
                Date = date,
                Count = byDate.GetValueOrDefault(date)
            });
        }

        return Ok(new CandidateStatisticsDto
        {
            From = from,
            To = to,
            TotalCandidates = totalCandidates,
            Days = days
        });
    }
    [HttpGet]
    public async Task<IActionResult> GetAccounts(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] string? keyword = null,
    [FromQuery] string? role = null,
    [FromQuery] string? status = null,
    [FromQuery] string? accountIds = null,
    [FromQuery] DateTime? createdFrom = null,
    [FromQuery] DateTime? createdTo = null)
    {
        var query = _context.Accounts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(a => a.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (createdFrom.HasValue)
        {
            var fromDate = createdFrom.Value.Date;

            query = query.Where(a => a.CreatedAt >= fromDate);
        }

        if (createdTo.HasValue)
        {
            var toDateExclusive = createdTo.Value.Date.AddDays(1);

            query = query.Where(a => a.CreatedAt < toDateExclusive);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lowerKeyword = keyword.ToLower();
            List<Guid> parsedIds = new();

            if (!string.IsNullOrEmpty(accountIds))
            {
                parsedIds = accountIds.Split(',')
                                      .Select(id => Guid.TryParse(id, out var parsedGuid) ? parsedGuid : Guid.Empty)
                                      .Where(id => id != Guid.Empty)
                                      .ToList();
            }

            query = query.Where(a => a.Email.ToLower().Contains(lowerKeyword) || parsedIds.Contains(a.AccountId));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new InternalAccountDto
            {
                AccountId = a.AccountId,
                Email = a.Email,
                Role = a.Role,
                Status = a.Status,
                IsEmailVerified = a.IsEmailVerified,
                IsFaceVerified = a.IsFaceVerified,
                IsVoiceVerified = a.IsVoiceVerified,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            })
            .ToListAsync();

        var result = new PagedResult<InternalAccountDto>
        {
            PageIndex = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items
        };

        return Ok(result);
    }

    // =========================================================
    // 2. CHI TIẾT ACCOUNT THEO ID
    // =========================================================
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAccountById(Guid id)
    {
        var account = await _context.Accounts
            .Where(a => a.AccountId == id)
            .Select(a => new {
                a.AccountId,
                a.Email,
                a.Role,
                a.Status,
                a.IsEmailVerified,
                a.IsFaceVerified,
                a.IsVoiceVerified,
                a.CreatedAt,
                a.UpdatedAt,
                a.LastLoginAt,
                a.BlockedReason,
                a.BlockedAt,
                a.BlockedBy
            })
            .FirstOrDefaultAsync();

        if (account == null)
        {
            return NotFound();
        }

        return Ok(account);
    }

    // =========================================================
    // 3. CẬP NHẬT TRẠNG THÁI + LƯU LỊCH SỬ KHÓA VÀO DATABASE
    // =========================================================
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromQuery] Guid? adminId, [FromBody] AdminUpdateUserStatusDto dto)
    {
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountId == id);
        if (account == null) return NotFound();

        account.Status = dto.Status;
        account.UpdatedAt = VietnamTime.Now;
        if (dto.Status == "Blocked")
        {
            account.BlockedReason = dto.Reason;
            account.BlockedAt = VietnamTime.Now;
            account.BlockedBy = adminId;

            var tokensToDelete = _context.RefreshTokens.Where(rt => rt.AccountId == id);
            _context.RefreshTokens.RemoveRange(tokensToDelete);
        }
        else
        {
            account.BlockedReason = null;
            account.BlockedAt = null;
            account.BlockedBy = null;
        }

        var history = new AccountBlockHistory
        {
            BlockHistoryId = Guid.NewGuid(),
            AccountId = id,
            Action = dto.Status == "Blocked" ? "Block" : "Unblock",
            Reason = dto.Reason ?? (dto.Status == "Blocked" ? "Tài khoản bị khóa bởi Admin." : "Tài khoản được kích hoạt trở lại."),
            PerformedBy = adminId ?? Guid.Empty,
            CreatedAt = VietnamTime.Now
        };

        _context.AccountBlockHistories.Add(history);
        await _context.SaveChangesAsync();
        return Ok();
    }

    // =========================================================
    // 4. CẬP NHẬT VAI TRÒ
    // =========================================================
    [HttpPatch("{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] AdminUpdateUserRoleDto dto)
    {
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountId == id);
        if (account == null) return NotFound();

        account.Role = dto.Role;
        account.UpdatedAt = VietnamTime.Now;

        var tokensToDelete = _context.RefreshTokens.Where(rt => rt.AccountId == id);
        _context.RefreshTokens.RemoveRange(tokensToDelete);

        await _context.SaveChangesAsync();
        return Ok();
    }

    // =========================================================
    // 5. TỰ ĐỘNG KHÓA VÀ LƯU LỊCH SỬ AUTO-BLOCK
    // =========================================================
    //[HttpPost("auto-block-inactive")]
    //public async Task<IActionResult> AutoBlockInactive([FromQuery] int days = 30)
    //{
    //    var cutoffDate = VietnamTime.Now.AddDays(-days);

    //    var inactiveUsers = await _context.Accounts
    //        .Where(a => a.Role == "User" &&
    //                    a.Status == "Active" &&
    //                    (a.LastLoginAt == null ? a.CreatedAt : a.LastLoginAt) < cutoffDate)
    //        .ToListAsync();

    //    int blockedCount = 0;
    //    foreach (var user in inactiveUsers)
    //    {
    //        user.Status = "Blocked";
    //        var reason = $"System Auto-Blocked due to inactivity for {days} days.";
    //        user.BlockedReason = reason;
    //        user.BlockedAt = VietnamTime.Now;
    //        user.UpdatedAt = VietnamTime.Now;
    //        user.BlockedBy = Guid.Empty;
    //        blockedCount++;

    //        var autoHistory = new AccountBlockHistory
    //        {
    //            BlockHistoryId = Guid.NewGuid(),
    //            AccountId = user.AccountId,
    //            Action = "Block",
    //            Reason = reason,
    //            PerformedBy = Guid.Empty,
    //            CreatedAt = VietnamTime.Now
    //        };
    //        _context.AccountBlockHistories.Add(autoHistory);

    //        var tokensToDelete = _context.RefreshTokens.Where(rt => rt.AccountId == user.AccountId);
    //        _context.RefreshTokens.RemoveRange(tokensToDelete);
    //    }

    //    if (blockedCount > 0)
    //    {
    //        await _context.SaveChangesAsync();
    //    }

    //    return Ok(new
    //    {
    //        InactiveDays = days,
    //        BlockedCount = blockedCount
    //    });
    //}

    // =========================================================
    // 5. BLOCK HÀNG LOẠT THEO DANH SÁCH ACCOUNTID (do UserAPI tính toán gửi sang)
    // =========================================================
    [HttpPost("batch-block-inactive")]
    public async Task<IActionResult> BatchBlockInactive(
        [FromBody] List<Guid> accountIds,
        [FromQuery] int days = 30)
    {
        if (accountIds == null || !accountIds.Any())
        {
            return Ok(new { BlockedCount = 0 });
        }

        var users = await _context.Accounts
            .Where(a => accountIds.Contains(a.AccountId) &&
                        a.Role == "User" &&
                        a.Status == "Active")
            .ToListAsync();

        int blockedCount = 0;
        foreach (var user in users)
        {
            user.Status = "Blocked";
            var reason = $"System Auto-Blocked due to inactivity for {days} days.";
            user.BlockedReason = reason;
            user.BlockedAt = VietnamTime.Now;
            user.UpdatedAt = VietnamTime.Now;
            user.BlockedBy = Guid.Empty;
            blockedCount++;

            var autoHistory = new AccountBlockHistory
            {
                BlockHistoryId = Guid.NewGuid(),
                AccountId = user.AccountId,
                Action = "Block",
                Reason = reason,
                PerformedBy = Guid.Empty,
                CreatedAt = VietnamTime.Now
            };
            _context.AccountBlockHistories.Add(autoHistory);

            var tokensToDelete = _context.RefreshTokens.Where(rt => rt.AccountId == user.AccountId);
            _context.RefreshTokens.RemoveRange(tokensToDelete);
        }

        if (blockedCount > 0)
        {
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            InactiveDays = days,
            BlockedCount = blockedCount
        });
    }
}
public sealed class CandidateStatisticsDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public long TotalCandidates { get; set; }
    public List<CandidateStatisticsDayDto> Days { get; set; } = new();
}

public sealed class CandidateStatisticsDayDto
{
    public DateOnly Date { get; set; }
    public long Count { get; set; }
}

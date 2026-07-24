using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UserAPI.DTOs;
using UserAPI.Models;

namespace UserAPI.Controllers;

[ApiController]
[Route("api/admin/manager/users")]
[Authorize(Roles = "Admin,Manager")]
public class ManagerUsersController : ControllerBase
{
    private readonly UserDbContext _context;

    public ManagerUsersController(UserDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ManagerUserListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] string? role = null, [FromQuery] string? status = null, [FromQuery] DateTime? createdFrom = null, [FromQuery] DateTime? createdTo = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var connString = _context.Database.GetDbConnection().ConnectionString;
        
        var items = new List<ManagerUserListItemDto>();
        int totalCount = 0;

        using (var conn = new SqlConnection(connString))
        {
            await conn.OpenAsync();

            // Search condition
            var searchPattern = string.IsNullOrEmpty(keyword) ? null : $"%{keyword.Trim()}%";

            // Build WHERE clause
            var whereClauses = new List<string> { "(@Keyword IS NULL OR p.FullName LIKE @Keyword OR a.Email LIKE @Keyword)" };
            if (!string.IsNullOrWhiteSpace(role))
                whereClauses.Add("a.Role = @Role");
            if (!string.IsNullOrWhiteSpace(status))
                whereClauses.Add("a.Status = @Status");
            if (createdFrom.HasValue)
                whereClauses.Add("a.CreatedAt >= @CreatedFrom");
            if (createdTo.HasValue)
                whereClauses.Add("a.CreatedAt < DATEADD(day, 1, @CreatedTo)");
            var whereSql = string.Join(" AND ", whereClauses);

            // Total Count Query
            var countQuery = $@"
                SELECT COUNT(*)
                FROM DevPrep_User.dbo.UserProfiles p
                INNER JOIN DevPrep_Auth.dbo.Accounts a ON p.AccountId = a.AccountId
                WHERE {whereSql}";

            using (var cmd = new SqlCommand(countQuery, conn))
            {
                cmd.Parameters.AddWithValue("@Keyword", (object?)searchPattern ?? DBNull.Value);
                if (!string.IsNullOrWhiteSpace(role))
                    cmd.Parameters.AddWithValue("@Role", role.Trim());
                if (!string.IsNullOrWhiteSpace(status))
                    cmd.Parameters.AddWithValue("@Status", status.Trim());
                if (createdFrom.HasValue)
                    cmd.Parameters.AddWithValue("@CreatedFrom", createdFrom.Value);
                if (createdTo.HasValue)
                    cmd.Parameters.AddWithValue("@CreatedTo", createdTo.Value);
                var countResult = await cmd.ExecuteScalarAsync();
                totalCount = countResult == null || countResult == DBNull.Value ? 0 : Convert.ToInt32(countResult);
            }

            // Paginated List Query
            var listQuery = $@"
                SELECT 
                    p.UserId,
                    p.FullName,
                    a.Email,
                    a.Role,
                    a.Status,
                    a.CreatedAt AS RegistrationTimestamp,
                    (SELECT COUNT(*) FROM DevPrep_Interview.dbo.InterviewSessions s WHERE s.UserId = p.UserId AND s.IsDeleted = 0) AS TotalInterviewCount
                FROM DevPrep_User.dbo.UserProfiles p
                INNER JOIN DevPrep_Auth.dbo.Accounts a ON p.AccountId = a.AccountId
                WHERE {whereSql}
                ORDER BY a.CreatedAt DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            using (var cmd = new SqlCommand(listQuery, conn))
            {
                cmd.Parameters.AddWithValue("@Keyword", (object?)searchPattern ?? DBNull.Value);
                if (!string.IsNullOrWhiteSpace(role))
                    cmd.Parameters.AddWithValue("@Role", role.Trim());
                if (!string.IsNullOrWhiteSpace(status))
                    cmd.Parameters.AddWithValue("@Status", status.Trim());
                if (createdFrom.HasValue)
                    cmd.Parameters.AddWithValue("@CreatedFrom", createdFrom.Value);
                if (createdTo.HasValue)
                    cmd.Parameters.AddWithValue("@CreatedTo", createdTo.Value);
                cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        items.Add(new ManagerUserListItemDto
                        {
                            UserId = reader.GetGuid(0),
                            FullName = reader.IsDBNull(1) ? null : reader.GetString(1),
                            Email = reader.IsDBNull(2) ? null : reader.GetString(2),
                            Role = reader.IsDBNull(3) ? null : reader.GetString(3),
                            Status = reader.IsDBNull(4) ? null : reader.GetString(4),
                            RegistrationTimestamp = reader.GetDateTime(5),
                            TotalInterviewCount = reader.GetInt32(6)
                        });
                    }
                }
            }
        }

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var pagedResult = new PagedResult<ManagerUserListItemDto>
        {
            PageIndex = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items
        };

        return Ok(ApiResponse<PagedResult<ManagerUserListItemDto>>.Ok(pagedResult));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ManagerUserDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetails(Guid id)
    {
        var connString = _context.Database.GetDbConnection().ConnectionString;
        ManagerUserDetailsDto? details = null;

        using (var conn = new SqlConnection(connString))
        {
            await conn.OpenAsync();

            // 1. Get base Profile + Account info
            var profileQuery = @"
                SELECT 
                    p.UserId,
                    p.AccountId,
                    p.FullName,
                    p.AvatarUrl,
                    a.Email,
                    a.Role,
                    a.Status,
                    a.IsEmailVerified,
                    a.IsFaceVerified,
                    a.IsVoiceVerified,
                    a.CreatedAt AS RegistrationTimestamp,
                    a.UpdatedAt,
                    a.LastLoginAt,
                    a.BlockedReason,
                    a.BlockedAt
                FROM DevPrep_User.dbo.UserProfiles p
                INNER JOIN DevPrep_Auth.dbo.Accounts a ON p.AccountId = a.AccountId
                WHERE p.UserId = @UserId";

            using (var cmd = new SqlCommand(profileQuery, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", id);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        details = new ManagerUserDetailsDto
                        {
                            UserId = reader.GetGuid(0),
                            AccountId = reader.GetGuid(1),
                            FullName = reader.IsDBNull(2) ? null : reader.GetString(2),
                            AvatarUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                            Email = reader.IsDBNull(4) ? null : reader.GetString(4),
                            Role = reader.IsDBNull(5) ? null : reader.GetString(5),
                            Status = reader.IsDBNull(6) ? null : reader.GetString(6),
                            IsEmailVerified = reader.IsDBNull(7) ? false : reader.GetBoolean(7),
                            IsFaceVerified = reader.IsDBNull(8) ? false : reader.GetBoolean(8),
                            IsVoiceVerified = reader.IsDBNull(9) ? false : reader.GetBoolean(9),
                            RegistrationTimestamp = reader.GetDateTime(10),
                            UpdatedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                            LastLoginAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                            BlockedReason = reader.IsDBNull(13) ? null : reader.GetString(13),
                            BlockedAt = reader.IsDBNull(14) ? null : reader.GetDateTime(14)
                        };
                    }
                }
            }

            if (details == null)
            {
                return NotFound(ApiResponse<object>.Fail("Candidate profile not found."));
            }

            // 2. Get Credit Balance (from CreditWallets)
            var balanceQuery = @"
                SELECT Balance 
                FROM DevPrep_Payment.dbo.CreditWallets 
                WHERE UserId = @UserId";

            using (var cmd = new SqlCommand(balanceQuery, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", id);
                var balanceObj = await cmd.ExecuteScalarAsync();
                details.CreditBalance = balanceObj != null && balanceObj != DBNull.Value ? (int)balanceObj : 0;
            }

            // 3. Get Credit transactions (from CreditLedger)
            var ledgerQuery = @"
                SELECT LedgerId, EntryType, CreditChange, BalanceAfter, Description, CreatedAt
                FROM DevPrep_Payment.dbo.CreditLedger
                WHERE UserId = @UserId
                ORDER BY CreatedAt DESC";

            using (var cmd = new SqlCommand(ledgerQuery, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", id);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        details.CreditTransactions.Add(new CreditTransactionDto
                        {
                            LedgerId = reader.GetGuid(0),
                            EntryType = reader.IsDBNull(1) ? null : reader.GetString(1),
                            CreditChange = reader.GetInt32(2),
                            BalanceAfter = reader.GetInt32(3),
                            Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                            CreatedAt = reader.GetDateTime(5)
                        });
                    }
                }
            }

            // 4. Get Completed sessions (from InterviewSessions)
            var sessionsQuery = @"
                SELECT SessionId, Title, Level, InteractionMode, Status, StartedAt, DurationMinutes, TotalQuestions, AnsweredQuestions, CreditCost
                FROM DevPrep_Interview.dbo.InterviewSessions
                WHERE UserId = @UserId AND Status = 'Completed' AND IsDeleted = 0
                ORDER BY CreatedAt DESC";

            using (var cmd = new SqlCommand(sessionsQuery, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", id);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        details.CompletedSessions.Add(new CompletedSessionDto
                        {
                            SessionId = reader.GetGuid(0),
                            Title = reader.IsDBNull(1) ? null : reader.GetString(1),
                            Level = reader.IsDBNull(2) ? null : reader.GetString(2),
                            InteractionMode = reader.IsDBNull(3) ? null : reader.GetString(3),
                            Status = reader.IsDBNull(4) ? null : reader.GetString(4),
                            StartedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                            DurationMinutes = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                            TotalQuestions = reader.GetInt32(7),
                            AnsweredQuestions = reader.GetInt32(8),
                            CreditCost = reader.GetInt32(9)
                        });
                    }
                }
            }
        }

        return Ok(ApiResponse<ManagerUserDetailsDto>.Ok(details));
    }
}

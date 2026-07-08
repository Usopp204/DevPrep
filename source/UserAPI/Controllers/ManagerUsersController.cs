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
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null)
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

            // Total Count Query
            var countQuery = @"
                SELECT COUNT(*)
                FROM DevPrep_User.dbo.UserProfiles p
                INNER JOIN DevPrep_Auth.dbo.Accounts a ON p.AccountId = a.AccountId
                WHERE (@Keyword IS NULL OR p.FullName LIKE @Keyword OR a.Email LIKE @Keyword)";

            using (var cmd = new SqlCommand(countQuery, conn))
            {
                cmd.Parameters.AddWithValue("@Keyword", (object?)searchPattern ?? DBNull.Value);
                var countResult = await cmd.ExecuteScalarAsync();
                totalCount = countResult == null || countResult == DBNull.Value ? 0 : Convert.ToInt32(countResult);
            }

            // Paginated List Query
            var listQuery = @"
                SELECT 
                    p.UserId,
                    p.FullName,
                    a.Email,
                    a.Status,
                    a.CreatedAt AS RegistrationTimestamp,
                    (SELECT COUNT(*) FROM DevPrep_Interview.dbo.InterviewSessions s WHERE s.UserId = p.UserId AND s.IsDeleted = 0) AS TotalInterviewCount
                FROM DevPrep_User.dbo.UserProfiles p
                INNER JOIN DevPrep_Auth.dbo.Accounts a ON p.AccountId = a.AccountId
                WHERE (@Keyword IS NULL OR p.FullName LIKE @Keyword OR a.Email LIKE @Keyword)
                ORDER BY a.CreatedAt DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            using (var cmd = new SqlCommand(listQuery, conn))
            {
                cmd.Parameters.AddWithValue("@Keyword", (object?)searchPattern ?? DBNull.Value);
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
                            Status = reader.IsDBNull(3) ? null : reader.GetString(3),
                            RegistrationTimestamp = reader.GetDateTime(4),
                            TotalInterviewCount = reader.GetInt32(5)
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
                    a.Status,
                    a.CreatedAt AS RegistrationTimestamp
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
                            Status = reader.IsDBNull(5) ? null : reader.GetString(5),
                            RegistrationTimestamp = reader.GetDateTime(6)
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
                SELECT SessionId, Title, QuestionType, StartedAt, EndedAt, TotalQuestions, AnsweredQuestions, CreditCost
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
                            QuestionType = reader.IsDBNull(2) ? null : reader.GetString(2),
                            StartedAt = reader.GetDateTime(3),
                            EndedAt = reader.GetDateTime(4),
                            TotalQuestions = reader.GetInt32(5),
                            AnsweredQuestions = reader.GetInt32(6),
                            CreditCost = reader.GetInt32(7)
                        });
                    }
                }
            }
        }

        return Ok(ApiResponse<ManagerUserDetailsDto>.Ok(details));
    }
}

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UserAPI.DTOs;
using UserAPI.Models;
using UserAPI.Repositories.Interfaces;

namespace UserAPI.Repositories;

public class AdminUserRepository : IAdminUserRepository
{
    private readonly UserDbContext _context;
    private readonly IConfiguration _configuration;

    public AdminUserRepository(UserDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(AdminUserQueryDto query)
    {
        if (query.Page < 1)
            query.Page = 1;

        if (query.PageSize < 1 || query.PageSize > 100)
            query.PageSize = 10;

        var keyword = string.IsNullOrWhiteSpace(query.Keyword)
            ? null
            : $"%{query.Keyword.Trim()}%";

        var role = string.IsNullOrWhiteSpace(query.Role)
            ? null
            : query.Role.Trim();

        var status = string.IsNullOrWhiteSpace(query.Status)
            ? null
            : query.Status.Trim();

        var items = new List<AdminUserListItemDto>();
        var totalCount = 0;

        var connString = _configuration.GetValue<string>("USER_DB_CONNECTION_STRING");

        await using var conn = new SqlConnection(connString);
        await conn.OpenAsync();

        var countSql = @"
            SELECT COUNT(1)
            FROM DevPrep_Auth.dbo.Accounts a
            LEFT JOIN DevPrep_User.dbo.UserProfiles p
                ON p.AccountId = a.AccountId
            WHERE
                (@Keyword IS NULL OR p.FullName LIKE @Keyword OR a.Email LIKE @Keyword)
                AND (@Role IS NULL OR a.Role = @Role)
                AND (@Status IS NULL OR a.Status = @Status)
                AND (@CreatedFrom IS NULL OR a.CreatedAt >= @CreatedFrom)
                AND (@CreatedTo IS NULL OR a.CreatedAt < DATEADD(day, 1, @CreatedTo));";

        await using (var cmd = new SqlCommand(countSql, conn))
        {
            AddCommonQueryParameters(cmd, keyword, role, status, query.CreatedFrom, query.CreatedTo);

            var countResult = await cmd.ExecuteScalarAsync();

            totalCount = countResult == null || countResult == DBNull.Value
                ? 0
                : Convert.ToInt32(countResult);
        }

        var listSql = @"
            SELECT
                COALESCE(p.UserId, a.AccountId) AS UserId,
                a.AccountId,
                p.FullName,
                a.Email,
                a.Role,
                a.Status,
                a.IsEmailVerified,
                a.CreatedAt,
                a.UpdatedAt,
                COALESCE((
                    SELECT COUNT(1)
                    FROM DevPrep_Interview.dbo.InterviewSessions s
                    WHERE p.UserId IS NOT NULL
                      AND s.UserId = p.UserId
                      AND ISNULL(s.IsDeleted, 0) = 0
                ), 0) AS TotalSessionCount,
                COALESCE((
                    SELECT TOP 1 w.Balance
                    FROM DevPrep_Payment.dbo.CreditWallets w
                    WHERE p.UserId IS NOT NULL
                      AND w.UserId = p.UserId
                ), 0) AS CreditBalance
            FROM DevPrep_Auth.dbo.Accounts a
            LEFT JOIN DevPrep_User.dbo.UserProfiles p
                ON p.AccountId = a.AccountId
            WHERE
                (@Keyword IS NULL OR p.FullName LIKE @Keyword OR a.Email LIKE @Keyword)
                AND (@Role IS NULL OR a.Role = @Role)
                AND (@Status IS NULL OR a.Status = @Status)
                AND (@CreatedFrom IS NULL OR a.CreatedAt >= @CreatedFrom)
                AND (@CreatedTo IS NULL OR a.CreatedAt < DATEADD(day, 1, @CreatedTo))
            ORDER BY a.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        await using (var cmd = new SqlCommand(listSql, conn))
        {
            AddCommonQueryParameters(cmd, keyword, role, status, query.CreatedFrom, query.CreatedTo);

            cmd.Parameters.AddWithValue("@Offset", (query.Page - 1) * query.PageSize);
            cmd.Parameters.AddWithValue("@PageSize", query.PageSize);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                items.Add(new AdminUserListItemDto
                {
                    UserId = reader.GetGuid(0),
                    AccountId = reader.GetGuid(1),
                    FullName = GetNullableString(reader, 2),
                    Email = GetNullableString(reader, 3),
                    Role = GetNullableString(reader, 4),
                    Status = GetNullableString(reader, 5),
                    IsEmailVerified = GetBool(reader, 6),
                    CreatedAt = reader.GetDateTime(7),
                    UpdatedAt = GetNullableDateTime(reader, 8),
                    TotalSessionCount = GetInt(reader, 9),
                    CreditBalance = GetInt(reader, 10)
                });
            }
        }

        var totalPages = (int)Math.Ceiling((double)totalCount / query.PageSize);

        return new PagedResult<AdminUserListItemDto>
        {
            PageIndex = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items
        };
    }

    public async Task<AdminUserDetailDto?> GetUserDetailAsync(Guid id)
    {
        var connString = _configuration.GetValue<string>("USER_DB_CONNECTION_STRING");

        AdminUserDetailDto? detail = null;
        Guid? profileUserId = null;

        await using var conn = new SqlConnection(connString);
        await conn.OpenAsync();

        var detailSql = @"
            SELECT
                p.UserId,
                a.AccountId,
                p.FullName,
                p.AvatarUrl,
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
            FROM DevPrep_Auth.dbo.Accounts a
            LEFT JOIN DevPrep_User.dbo.UserProfiles p
                ON p.AccountId = a.AccountId
            WHERE a.AccountId = @Id OR p.UserId = @Id;";

        await using (var cmd = new SqlCommand(detailSql, conn))
        {
            cmd.Parameters.AddWithValue("@Id", id);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                profileUserId = reader.IsDBNull(0) ? null : reader.GetGuid(0);
                var accountId = reader.GetGuid(1);

                detail = new AdminUserDetailDto
                {
                    UserId = profileUserId ?? accountId,
                    AccountId = accountId,
                    FullName = GetNullableString(reader, 2),
                    AvatarUrl = GetNullableString(reader, 3),
                    Email = GetNullableString(reader, 4),
                    Role = GetNullableString(reader, 5),
                    Status = GetNullableString(reader, 6),
                    IsEmailVerified = GetBool(reader, 7),
                    IsFaceVerified = GetBool(reader, 8),
                    IsVoiceVerified = GetBool(reader, 9),
                    CreatedAt = reader.GetDateTime(10),
                    UpdatedAt = GetNullableDateTime(reader, 11),
                    LastLoginAt = GetNullableDateTime(reader, 12),
                    BlockedReason = GetNullableString(reader, 13),
                    BlockedAt = GetNullableDateTime(reader, 14),
                    BlockedBy = reader.IsDBNull(15) ? null : reader.GetGuid(15),
                    CurrentCreditBalance = 0
                };
            }
        }

        if (detail == null)
            return null;

        if (!profileUserId.HasValue)
            return detail;

        var creditSql = @"
            SELECT TOP 1 Balance
            FROM DevPrep_Payment.dbo.CreditWallets
            WHERE UserId = @UserId;";

        await using (var cmd = new SqlCommand(creditSql, conn))
        {
            cmd.Parameters.AddWithValue("@UserId", profileUserId.Value);

            var balanceObj = await cmd.ExecuteScalarAsync();

            detail.CurrentCreditBalance = balanceObj == null || balanceObj == DBNull.Value
                ? 0
                : Convert.ToInt32(balanceObj);
        }

        var sessionSql = @"
            SELECT
                SessionId,
                Title,
                DurationMinutes,
                Level,
                InteractionMode,
                QuestionType,
                Status,
                StartedAt,
                EndedAt,
                TotalQuestions,
                AnsweredQuestions,
                AvgResponseTimeMs,
                CreditCost
            FROM DevPrep_Interview.dbo.InterviewSessions
            WHERE UserId = @UserId
              AND ISNULL(IsDeleted, 0) = 0
            ORDER BY CreatedAt DESC;";

        await using (var cmd = new SqlCommand(sessionSql, conn))
        {
            cmd.Parameters.AddWithValue("@UserId", profileUserId.Value);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                detail.Sessions.Add(new AdminUserSessionDto
                {
                    SessionId = reader.GetGuid(0),
                    Title = GetNullableString(reader, 1),
                    DurationMinutes = GetNullableInt(reader, 2),
                    Level = GetNullableString(reader, 3),
                    InteractionMode = GetNullableString(reader, 4),
                    QuestionType = GetNullableString(reader, 5),
                    Status = GetNullableString(reader, 6),
                    StartedAt = GetNullableDateTime(reader, 7),
                    EndedAt = GetNullableDateTime(reader, 8),
                    TotalQuestions = GetInt(reader, 9),
                    AnsweredQuestions = GetInt(reader, 10),
                    AvgResponseTimeMs = GetNullableInt(reader, 11),
                    CreditCost = GetInt(reader, 12)
                });
            }
        }

        return detail;
    }

    public async Task<bool> UpdateStatusAsync(Guid id, AdminUpdateUserStatusDto dto, Guid? adminAccountId)
    {
        var connString = _configuration.GetValue<string>("USER_DB_CONNECTION_STRING");

        await using var conn = new SqlConnection(connString);
        await conn.OpenAsync();

        var sql = @"
            DECLARE @TargetAccountId UNIQUEIDENTIFIER;

            SELECT TOP 1 @TargetAccountId = a.AccountId
            FROM DevPrep_Auth.dbo.Accounts a
            LEFT JOIN DevPrep_User.dbo.UserProfiles p
                ON p.AccountId = a.AccountId
            WHERE a.AccountId = @Id OR p.UserId = @Id;

            IF @TargetAccountId IS NULL
            BEGIN
                SELECT CAST(0 AS INT);
                RETURN;
            END;

            UPDATE DevPrep_Auth.dbo.Accounts
            SET
                Status = @Status,
                BlockedReason = CASE WHEN @Status = 'Blocked' THEN @Reason ELSE NULL END,
                BlockedAt = CASE WHEN @Status = 'Blocked' THEN SYSUTCDATETIME() ELSE NULL END,
                BlockedBy = CASE WHEN @Status = 'Blocked' THEN @BlockedBy ELSE NULL END,
                UpdatedAt = SYSUTCDATETIME()
            WHERE AccountId = @TargetAccountId;

            SELECT CAST(1 AS INT);";

        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Status", dto.Status.Trim());
        cmd.Parameters.AddWithValue("@Reason", (object?)dto.Reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BlockedBy", (object?)adminAccountId ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync();

        return result != null && result != DBNull.Value && Convert.ToInt32(result) == 1;
    }

    public async Task<bool> UpdateRoleAsync(Guid id, AdminUpdateUserRoleDto dto)
    {
        var connString = _configuration.GetValue<string>("USER_DB_CONNECTION_STRING");

        await using var conn = new SqlConnection(connString);
        await conn.OpenAsync();

        var sql = @"
            DECLARE @TargetAccountId UNIQUEIDENTIFIER;

            SELECT TOP 1 @TargetAccountId = a.AccountId
            FROM DevPrep_Auth.dbo.Accounts a
            LEFT JOIN DevPrep_User.dbo.UserProfiles p
                ON p.AccountId = a.AccountId
            WHERE a.AccountId = @Id OR p.UserId = @Id;

            IF @TargetAccountId IS NULL
            BEGIN
                SELECT CAST(0 AS INT);
                RETURN;
            END;

            UPDATE DevPrep_Auth.dbo.Accounts
            SET
                Role = @Role,
                UpdatedAt = SYSUTCDATETIME()
            WHERE AccountId = @TargetAccountId;

            SELECT CAST(1 AS INT);";

        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Role", dto.Role.Trim());

        var result = await cmd.ExecuteScalarAsync();

        return result != null && result != DBNull.Value && Convert.ToInt32(result) == 1;
    }

    private static void AddCommonQueryParameters(
        SqlCommand cmd,
        string? keyword,
        string? role,
        string? status,
        DateTime? createdFrom,
        DateTime? createdTo)
    {
        cmd.Parameters.AddWithValue("@Keyword", (object?)keyword ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Role", (object?)role ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", (object?)status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedFrom", (object?)createdFrom ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedTo", (object?)createdTo ?? DBNull.Value);
    }

    private static string? GetNullableString(SqlDataReader reader, int index)
    {
        return reader.IsDBNull(index) ? null : reader.GetString(index);
    }

    private static DateTime? GetNullableDateTime(SqlDataReader reader, int index)
    {
        return reader.IsDBNull(index) ? null : reader.GetDateTime(index);
    }

    private static int GetInt(SqlDataReader reader, int index)
    {
        return reader.IsDBNull(index) ? 0 : Convert.ToInt32(reader.GetValue(index));
    }

    private static int? GetNullableInt(SqlDataReader reader, int index)
    {
        return reader.IsDBNull(index) ? null : Convert.ToInt32(reader.GetValue(index));
    }

    private static bool GetBool(SqlDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
            return false;

        return Convert.ToBoolean(reader.GetValue(index));
    }
    public async Task<AdminAutoBlockInactiveResultDto> AutoBlockInactiveUsersAsync(int inactiveDays)
    {
        if (inactiveDays < 1)
            inactiveDays = 30;

        var connString = _configuration.GetValue<string>("USER_DB_CONNECTION_STRING");

        var result = new AdminAutoBlockInactiveResultDto
        {
            InactiveDays = inactiveDays,
            ExecutedAt = DateTime.UtcNow
        };

        await using var conn = new SqlConnection(connString);
        await conn.OpenAsync();

        var sql = @"
        DECLARE @CutoffDate DATETIME2 = DATEADD(day, -@InactiveDays, SYSUTCDATETIME());

        DECLARE @Targets TABLE
        (
            AccountId UNIQUEIDENTIFIER,
            UserId UNIQUEIDENTIFIER NULL,
            FullName NVARCHAR(150) NULL,
            Email NVARCHAR(320) NULL,
            LastActivityAt DATETIME2 NULL
        );

        INSERT INTO @Targets
        (
            AccountId,
            UserId,
            FullName,
            Email,
            LastActivityAt
        )
        SELECT
            a.AccountId,
            p.UserId,
            p.FullName,
            a.Email,
            lastActivity.LastActivityAt
        FROM DevPrep_Auth.dbo.Accounts a
        LEFT JOIN DevPrep_User.dbo.UserProfiles p
            ON p.AccountId = a.AccountId
        OUTER APPLY
        (
            SELECT MAX(SessionActivityAt) AS LastSessionAt
            FROM
            (
                SELECT s.StartedAt AS SessionActivityAt
                FROM DevPrep_Interview.dbo.InterviewSessions s
                WHERE p.UserId IS NOT NULL
                  AND s.UserId = p.UserId
                  AND ISNULL(s.IsDeleted, 0) = 0

                UNION ALL

                SELECT s.EndedAt
                FROM DevPrep_Interview.dbo.InterviewSessions s
                WHERE p.UserId IS NOT NULL
                  AND s.UserId = p.UserId
                  AND ISNULL(s.IsDeleted, 0) = 0

                UNION ALL

                SELECT s.CreatedAt
                FROM DevPrep_Interview.dbo.InterviewSessions s
                WHERE p.UserId IS NOT NULL
                  AND s.UserId = p.UserId
                  AND ISNULL(s.IsDeleted, 0) = 0

                UNION ALL

                SELECT s.UpdatedAt
                FROM DevPrep_Interview.dbo.InterviewSessions s
                WHERE p.UserId IS NOT NULL
                  AND s.UserId = p.UserId
                  AND ISNULL(s.IsDeleted, 0) = 0
            ) sessionDates
            WHERE SessionActivityAt IS NOT NULL
        ) sessionActivity
        OUTER APPLY
        (
            SELECT MAX(ActivityAt) AS LastActivityAt
            FROM
            (
                VALUES
                    (a.LastLoginAt),
                    (a.UpdatedAt),
                    (a.CreatedAt),
                    (p.UpdatedAt),
                    (p.CreatedAt),
                    (sessionActivity.LastSessionAt)
            ) AS activityValues(ActivityAt)
            WHERE ActivityAt IS NOT NULL
        ) lastActivity
        WHERE
            a.Role = 'User'
            AND a.Status = 'Active'
            AND lastActivity.LastActivityAt < @CutoffDate;

        UPDATE a
        SET
            a.Status = 'Blocked',
            a.BlockedReason = CONCAT('Auto-blocked due to inactivity for ', @InactiveDays, ' days.'),
            a.BlockedAt = SYSUTCDATETIME(),
            a.BlockedBy = NULL,
            a.UpdatedAt = SYSUTCDATETIME()
        FROM DevPrep_Auth.dbo.Accounts a
        INNER JOIN @Targets t
            ON t.AccountId = a.AccountId;

        SELECT
            AccountId,
            UserId,
            FullName,
            Email,
            LastActivityAt
        FROM @Targets
        ORDER BY LastActivityAt ASC;
    ";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@InactiveDays", inactiveDays);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.BlockedUsers.Add(new AdminAutoBlockedUserDto
            {
                AccountId = reader.GetGuid(0),
                UserId = reader.IsDBNull(1) ? null : reader.GetGuid(1),
                FullName = reader.IsDBNull(2) ? null : reader.GetString(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                LastActivityAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
            });
        }

        result.BlockedCount = result.BlockedUsers.Count;

        return result;
    }
}
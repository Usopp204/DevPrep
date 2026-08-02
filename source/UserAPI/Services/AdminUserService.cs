

using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using UserAPI.DTOs;
using UserAPI.Helpers;
using UserAPI.Repositories.Interfaces;
using UserAPI.Services.Interfaces;

namespace UserAPI.Services;

public class AdminUserService : IAdminUserService
{
    private readonly IAdminUserRepository _repository;
    private readonly IHttpClientFactory _clientFactory;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(
        IAdminUserRepository repository,
        IHttpClientFactory clientFactory,
        ILogger<AdminUserService> logger)
    {
        _repository = repository;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task<CandidateStatisticsDto> GetCandidateStatisticsAsync(
        DateOnly from,
        DateOnly to,
        string authorizationHeader,
        CancellationToken cancellationToken)
    {
        var vietnamOffset = TimeSpan.FromHours(7);
        var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), vietnamOffset).UtcDateTime;
        var toExclusiveUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), vietnamOffset).UtcDateTime;
        var url = $"/api/internal/accounts/statistics/candidates" +
                  $"?fromUtc={Uri.EscapeDataString(fromUtc.ToString("O"))}" +
                  $"&toExclusiveUtc={Uri.EscapeDataString(toExclusiveUtc.ToString("O"))}";

        var client = _clientFactory.CreateClient("AuthServiceClient");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("AuthAPI candidate statistics request failed.", null, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CandidateStatisticsDto>(
            cancellationToken: cancellationToken);
        if (result is null || result.From != from || result.To != to)
            throw new InvalidOperationException("AuthAPI returned an invalid candidate statistics response.");

        return result;
    }
    // =========================================================
    // DANH SÁCH USER (HTTP AGGREGATION) -  TÌM THEO TÊN
    // =========================================================
    public async Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(AdminUserQueryDto query)
    {
        query.Page = query.Page < 1 ? 1 : query.Page;
        query.PageSize = (query.PageSize < 1 || query.PageSize > 100) ? 10 : query.PageSize;

        var authClient = _clientFactory.CreateClient("AuthServiceClient");
        var interviewClient = _clientFactory.CreateClient("InterviewServiceClient");
        var paymentClient = _clientFactory.CreateClient("PaymentServiceClient");

        //Hỗ trợ tìm kiếm theo FullName tại Database cục bộ (UserAPI) trước
        string? matchedAccountIdsParam = null;
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            // Tìm kiếm các Profiles trùng khớp tên trong DB của UserAPI
            var matchedAccountIds = await _repository.GetAccountIdsByNameKeywordAsync(query.Keyword);
            if (matchedAccountIds != null && matchedAccountIds.Any())
            {
                matchedAccountIdsParam = string.Join(",", matchedAccountIds);
            }
        }

        // 1. Gọi AuthAPI lấy danh sách Accounts (Có gửi kèm list accountIds lọc theo tên nếu có)
        var authUrl = $"/api/internal/accounts?page={query.Page}&pageSize={query.PageSize}" +
                      $"&keyword={Uri.EscapeDataString(query.Keyword ?? "")}" +
                      $"&role={query.Role ?? ""}&status={query.Status ?? ""}";

        if (!string.IsNullOrEmpty(matchedAccountIdsParam))
        {
            authUrl += $"&accountIds={matchedAccountIdsParam}";
        }

        HttpResponseMessage authResponse;
        try
        {
            authResponse = await authClient.GetAsync(authUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể kết nối đến Auth-API tại đường dẫn: {Url}", authUrl);
            throw new Exception($"Lỗi kết nối đến Auth-API: {ex.Message}");
        }

        if (!authResponse.IsSuccessStatusCode)
        {
            var errorContent = await authResponse.Content.ReadAsStringAsync();
            _logger.LogError("Auth-API trả về mã lỗi: {StatusCode}. Chi tiết: {Content}", authResponse.StatusCode, errorContent);
            throw new Exception($"[LỖI GỌI AUTH-API] Mã lỗi: {(int)authResponse.StatusCode}. Chi tiết: {errorContent}");
        }

        var jsonString = await authResponse.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            _logger.LogWarning("Auth-API trả về mã 200 OK nhưng body trống hoàn toàn.");
            return GetEmptyPagedResult(query.Page, query.PageSize);
        }

        PagedResult<InternalAccountDto>? authResult;
        try
        {
            authResult = System.Text.Json.JsonSerializer.Deserialize<PagedResult<InternalAccountDto>>(jsonString, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Lỗi phân tích cú pháp JSON từ Auth-API. Nội dung thô: {RawJson}", jsonString);
            throw new Exception($"Lỗi định dạng dữ liệu từ Auth-API: {ex.Message}. Nội dung nhận được: {jsonString}");
        }

        if (authResult == null || authResult.Items == null || !authResult.Items.Any())
        {
            return GetEmptyPagedResult(query.Page, query.PageSize);
        }

        var accounts = authResult.Items;
        var accountIds = accounts.Select(a => a.AccountId).ToList();

        // 2. Lấy thông tin UserProfiles tương ứng trong DB nội bộ
        var profiles = await _repository.GetProfilesByAccountIdsAsync(accountIds);
        var userIds = profiles.Select(p => p.UserId).ToList();

        // 3. Gọi song song (Parallel) sang Interview và Payment API lấy Session & Wallet
        List<InternalSessionSummaryDto> sessionsResponse = new();
        List<InternalWalletDto> walletsResponse = new();

        // Lưu ý: InterviewAPI đang lưu AccountId vào cột UserId của nó, nên phải gửi accountIds
        var sessionsTask = interviewClient.PostAsJsonAsync("/api/internal/interviews/sessions/summaries", accountIds);
        var walletsTask = paymentClient.PostAsJsonAsync("/api/internal/payments/wallets/balances", userIds);

        try
        {
            await Task.WhenAll(sessionsTask, walletsTask);

            if (sessionsTask.Result.IsSuccessStatusCode)
            {
                sessionsResponse = await sessionsTask.Result.Content.ReadFromJsonAsync<List<InternalSessionSummaryDto>>() ?? new();
            }
            else
            {
                var err = await sessionsTask.Result.Content.ReadAsStringAsync();
                _logger.LogWarning("Interview-API trả về lỗi: {Err}", err);
            }

            if (walletsTask.Result.IsSuccessStatusCode)
            {
                walletsResponse = await walletsTask.Result.Content.ReadFromJsonAsync<List<InternalWalletDto>>() ?? new();
            }
            else
            {
                var err = await walletsTask.Result.Content.ReadAsStringAsync();
                _logger.LogWarning("Payment-API trả về lỗi: {Err}", err);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi song song sang Interview/Payment API.");
        }

        // 4. Map & gộp dữ liệu
        var items = new List<AdminUserListItemDto>();
        foreach (var acc in accounts)
        {
            var profile = profiles.FirstOrDefault(p => p.AccountId == acc.AccountId);
            var userId = profile?.UserId ?? acc.AccountId;

            // Sửa: InterviewAPI trả về UserId thực chất là AccountId, nên phải so khớp theo acc.AccountId
            var sessionSummary = sessionsResponse.FirstOrDefault(s => s.UserId == acc.AccountId);
            var wallet = walletsResponse.FirstOrDefault(w => w.UserId == userId);

            items.Add(new AdminUserListItemDto
            {
                UserId = userId,
                AccountId = acc.AccountId,
                FullName = profile?.FullName,
                Email = acc.Email,
                Role = acc.Role,
                Status = acc.Status,
                IsEmailVerified = acc.IsEmailVerified,
                CreatedAt = acc.CreatedAt,
                UpdatedAt = acc.UpdatedAt,
                TotalSessionCount = sessionSummary?.TotalSessions ?? 0,
                CreditBalance = wallet?.Balance ?? 0
            });
        }

        return new PagedResult<AdminUserListItemDto>
        {
            PageIndex = authResult.PageIndex,
            PageSize = authResult.PageSize,
            TotalCount = authResult.TotalCount,
            TotalPages = authResult.TotalPages,
            Items = items
        };
    }

    // =========================================================
    // CHI TIẾT USER (HTTP AGGREGATION)
    // =========================================================
    public async Task<AdminUserDetailDto?> GetUserDetailAsync(Guid id)
    {
        var authClient = _clientFactory.CreateClient("AuthServiceClient");
        var interviewClient = _clientFactory.CreateClient("InterviewServiceClient");
        var paymentClient = _clientFactory.CreateClient("PaymentServiceClient");

        var profile = await _repository.GetProfileByUserIdAsync(id) ?? await _repository.GetProfileByAccountIdAsync(id);
        Guid accountId = profile?.AccountId ?? id;
        Guid userId = profile?.UserId ?? id;

        HttpResponseMessage accResponse;
        try
        {
            accResponse = await authClient.GetAsync($"/api/internal/accounts/{accountId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể kết nối đến Auth-API để lấy chi tiết cho AccountId: {Id}", accountId);
            return null;
        }

        if (!accResponse.IsSuccessStatusCode) return null;

        var accJson = await accResponse.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(accJson)) return null;

        var acc = System.Text.Json.JsonSerializer.Deserialize<InternalAccountDto>(accJson, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (acc == null) return null;

        List<AdminUserSessionDto> sessions = new();
        InternalWalletDto? wallet = null;

        var sessionsTask = interviewClient.GetAsync($"/api/internal/interviews/sessions?userId={accountId}");
        var walletTask = paymentClient.GetAsync($"/api/internal/payments/wallets?userId={userId}");

        try
        {
            await Task.WhenAll(sessionsTask, walletTask);

            var resSession = await sessionsTask;
            if (resSession.IsSuccessStatusCode)
            {
                sessions = await resSession.Content.ReadFromJsonAsync<List<AdminUserSessionDto>>() ?? new();
            }
            else
            {
                var errContent = await resSession.Content.ReadAsStringAsync();
                _logger.LogWarning("Không thể lấy danh sách Sessions của User {UserId}. Chi tiết: {Err}", userId, errContent);
            }

            var resWallet = await walletTask;
            if (resWallet.IsSuccessStatusCode)
            {
                wallet = await resWallet.Content.ReadFromJsonAsync<InternalWalletDto>();
            }
            else
            {
                var errContent = await resWallet.Content.ReadAsStringAsync();
                _logger.LogWarning("Không thể lấy thông tin ví của User {UserId}. Chi tiết: {Err}", userId, errContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra khi nạp thông tin chi tiết (Sessions/Wallet) của user {UserId}.", userId);
        }

        return new AdminUserDetailDto
        {
            UserId = userId,
            AccountId = accountId,
            FullName = profile?.FullName,
            AvatarUrl = profile?.AvatarUrl,
            Email = acc.Email,
            Role = acc.Role,
            Status = acc.Status,
            IsEmailVerified = acc.IsEmailVerified,
            IsFaceVerified = acc.IsFaceVerified,
            IsVoiceVerified = acc.IsVoiceVerified,
            CreatedAt = acc.CreatedAt,
            UpdatedAt = acc.UpdatedAt,
            LastLoginAt = acc.LastLoginAt,
            BlockedReason = acc.BlockedReason,
            BlockedAt = acc.BlockedAt,
            BlockedBy = acc.BlockedBy,
            CurrentCreditBalance = wallet?.Balance ?? 0,
            Sessions = sessions
        };
    }

    // =========================================================
    // CẬP NHẬT TRẠNG THÁI (STATUS) - CHẶN ADMIN TỰ KHÓA CHÍNH MÌNH
    // =========================================================
    public async Task<bool> UpdateStatusAsync(Guid id, AdminUpdateUserStatusDto dto, Guid? adminAccountId)
    {
        var profile = await _repository.GetProfileByUserIdAsync(id) ?? await _repository.GetProfileByAccountIdAsync(id);
        Guid accountId = profile?.AccountId ?? id;

        // Chặn đứng hành vi Admin tự khóa tài khoản của chính mình
        if (accountId == adminAccountId)
        {
            throw new InvalidOperationException(" Bạn không thể tự khóa tài khoản của chính mình!");
        }

        dto.Status = dto.Status?.Trim() ?? string.Empty;
        dto.Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();

        var allowedStatuses = new[] { "Active", "Blocked" };
        if (!allowedStatuses.Contains(dto.Status, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Status chỉ được là Active hoặc Blocked.");
        }

        dto.Status = allowedStatuses.First(x => string.Equals(x, dto.Status, StringComparison.OrdinalIgnoreCase));

        var authClient = _clientFactory.CreateClient("AuthServiceClient");
        var response = await authClient.PatchAsJsonAsync($"/api/internal/accounts/{accountId}/status?adminId={adminAccountId}", dto);
        return response.IsSuccessStatusCode;
    }

    // =========================================================
    // CẬP NHẬT VAI TRÒ (ROLE) - NHẬN 3 THAM SỐ
    // =========================================================
    public async Task<bool> UpdateRoleAsync(Guid id, AdminUpdateUserRoleDto dto, Guid? adminAccountId)
    {
        var profile = await _repository.GetProfileByUserIdAsync(id) ?? await _repository.GetProfileByAccountIdAsync(id);
        Guid accountId = profile?.AccountId ?? id;

        // Chặn đứng hành vi Admin tự thay đổi phân quyền (Role) của chính mình
        if (accountId == adminAccountId)
        {
            throw new InvalidOperationException(" Bạn không thể tự thay đổi vai trò (role) của chính mình!");
        }

        dto.Role = dto.Role?.Trim() ?? string.Empty;

        var allowedRoles = new[] { "Admin", "Manager", "User" };
        if (!allowedRoles.Contains(dto.Role, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Role chỉ được là Admin, Manager hoặc User.");
        }

        dto.Role = allowedRoles.First(x => string.Equals(x, dto.Role, StringComparison.OrdinalIgnoreCase));

        var authClient = _clientFactory.CreateClient("AuthServiceClient");

        // Gửi kèm thông tin adminId để AuthAPI biết ai đang thay đổi
        var response = await authClient.PatchAsJsonAsync($"/api/internal/accounts/{accountId}/role?adminId={adminAccountId}", dto);
        return response.IsSuccessStatusCode;
    }

    // =========================================================
    // TỰ ĐỘNG KHÓA NGƯỜI DÙNG KHÔNG HOẠT ĐỘNG (AUTO-BLOCK)
    // =========================================================
    //public async Task<AdminAutoBlockInactiveResultDto> AutoBlockInactiveUsersAsync(int inactiveDays)
    //{
    //    if (inactiveDays < 1) inactiveDays = 30;
    //    if (inactiveDays > 365) inactiveDays = 365;

    //    var authClient = _clientFactory.CreateClient("AuthServiceClient");
    //    var response = await authClient.PostAsync($"/api/internal/accounts/auto-block-inactive?days={inactiveDays}", null);

    //    if (response.IsSuccessStatusCode)
    //    {
    //        return await response.Content.ReadFromJsonAsync<AdminAutoBlockInactiveResultDto>() ?? new();
    //    }

    //    return new AdminAutoBlockInactiveResultDto { InactiveDays = inactiveDays };
    //}


    // =========================================================
    // TỰ ĐỘNG KHÓA NGƯỜI DÙNG KHÔNG HOẠT ĐỘNG (AUTO-BLOCK)
    // Điều kiện: KHÔNG login, KHÔNG interview, KHÔNG giao dịch trong N ngày
    // =========================================================
    public async Task<AdminAutoBlockInactiveResultDto> AutoBlockInactiveUsersAsync(int inactiveDays)
    {
        if (inactiveDays < 1) inactiveDays = 30;
        if (inactiveDays > 365) inactiveDays = 365;

        var cutoffDate = VietnamTime.Now.AddDays(-inactiveDays);

        var authClient = _clientFactory.CreateClient("AuthServiceClient");
        var interviewClient = _clientFactory.CreateClient("InterviewServiceClient");
        var paymentClient = _clientFactory.CreateClient("PaymentServiceClient");

        // 1. Lấy toàn bộ Active "User" accounts từ AuthAPI
        List<InternalAccountDto> accounts = new();
        try
        {
            var authResp = await authClient.GetAsync("/api/internal/accounts?role=User&status=Active&pageSize=100000");
            if (authResp.IsSuccessStatusCode)
            {
                var json = await authResp.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<PagedResult<InternalAccountDto>>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                accounts = result?.Items ?? new();
            }
            else
            {
                _logger.LogWarning("Auth-API trả lỗi khi lấy danh sách active users cho auto-block.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi Auth-API để lấy danh sách active users.");
            return new AdminAutoBlockInactiveResultDto { InactiveDays = inactiveDays, ExecutedAt = VietnamTime.Now };
        }

        if (!accounts.Any())
        {
            return new AdminAutoBlockInactiveResultDto { InactiveDays = inactiveDays, ExecutedAt = VietnamTime.Now };
        }

        var accountIds = accounts.Select(a => a.AccountId).ToList();
        var profiles = await _repository.GetProfilesByAccountIdsAsync(accountIds);

        // 2. Gọi song song Interview & Payment để lấy last-activity
        List<LastActivityDto> interviewActivity = new();
        List<LastActivityDto> paymentActivity = new();

        var interviewTask = interviewClient.PostAsJsonAsync("/api/internal/interviews/last-activity", accountIds);
        var userIds = profiles.Select(p => p.UserId).ToList();
        var paymentTask = paymentClient.PostAsJsonAsync("/api/internal/payments/last-activity", userIds);

        try
        {
            await Task.WhenAll(interviewTask, paymentTask);

            if (interviewTask.Result.IsSuccessStatusCode)
            {
                interviewActivity = await interviewTask.Result.Content.ReadFromJsonAsync<List<LastActivityDto>>() ?? new();
            }
            else
            {
                _logger.LogWarning("Interview-API trả lỗi khi lấy last-activity.");
            }

            if (paymentTask.Result.IsSuccessStatusCode)
            {
                paymentActivity = await paymentTask.Result.Content.ReadFromJsonAsync<List<LastActivityDto>>() ?? new();
            }
            else
            {
                _logger.LogWarning("Payment-API trả lỗi khi lấy last-activity.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi song song Interview/Payment API để lấy last-activity.");
        }

        // 3. Xét từng account: hoạt động cuối = max(LastLoginAt, LastInterview, LastPayment, CreatedAt)
        var toBlock = new List<Guid>();
        foreach (var acc in accounts)
        {
            var profile = profiles.FirstOrDefault(p => p.AccountId == acc.AccountId);
            var interviewLast = interviewActivity.FirstOrDefault(x => x.AccountId == acc.AccountId || x.UserId == acc.AccountId) ?.LastActivityAt;
            var paymentLast = profile != null
                ? paymentActivity.FirstOrDefault(x => x.UserId == profile.UserId)?.LastActivityAt
                : null;

            var candidates = new[] { acc.LastLoginAt, interviewLast, paymentLast, acc.CreatedAt }
                .Where(d => d.HasValue)
                .Select(d => d!.Value);

            var lastActivity = candidates.Any() ? candidates.Max() : acc.CreatedAt;

            if (lastActivity < cutoffDate)
            {
                toBlock.Add(acc.AccountId);
            }
        }

        if (!toBlock.Any())
        {
            return new AdminAutoBlockInactiveResultDto { InactiveDays = inactiveDays, ExecutedAt = VietnamTime.Now };
        }

        // 4. Gửi danh sách cụ thể sang AuthAPI để block
        int blockedCount = 0;
        try
        {
            var blockResp = await authClient.PostAsJsonAsync(
                $"/api/internal/accounts/batch-block-inactive?days={inactiveDays}", toBlock);

            if (blockResp.IsSuccessStatusCode)
            {
                blockedCount = toBlock.Count;
            }
            else
            {
                var err = await blockResp.Content.ReadAsStringAsync();
                _logger.LogError("Auth-API trả lỗi khi block hàng loạt. Chi tiết: {Err}", err);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi Auth-API để block hàng loạt users.");
        }

        return new AdminAutoBlockInactiveResultDto
        {
            InactiveDays = inactiveDays,
            BlockedCount = blockedCount,
            ExecutedAt = VietnamTime.Now
        };
    }

    private class LastActivityDto
    {
        public Guid AccountId { get; set; }
        public Guid UserId { get; set; }
        public DateTime? LastActivityAt { get; set; }
    }


    private PagedResult<AdminUserListItemDto> GetEmptyPagedResult(int page, int pageSize)
    {
        return new PagedResult<AdminUserListItemDto>
        {
            PageIndex = page,
            PageSize = pageSize,
            TotalCount = 0,
            TotalPages = 0,
            Items = new List<AdminUserListItemDto>()
        };
    }
}
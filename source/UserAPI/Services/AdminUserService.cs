

using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using UserAPI.DTOs;
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

        var sessionsTask = interviewClient.PostAsJsonAsync("/api/internal/interviews/sessions/summaries", userIds);
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

            var sessionSummary = sessionsResponse.FirstOrDefault(s => s.UserId == userId);
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

        var sessionsTask = interviewClient.GetAsync($"/api/internal/interviews/sessions?userId={userId}");
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
    public async Task<AdminAutoBlockInactiveResultDto> AutoBlockInactiveUsersAsync(int inactiveDays)
    {
        if (inactiveDays < 1) inactiveDays = 30;
        if (inactiveDays > 365) inactiveDays = 365;

        var authClient = _clientFactory.CreateClient("AuthServiceClient");
        var response = await authClient.PostAsync($"/api/internal/accounts/auto-block-inactive?days={inactiveDays}", null);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AdminAutoBlockInactiveResultDto>() ?? new();
        }

        return new AdminAutoBlockInactiveResultDto { InactiveDays = inactiveDays };
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
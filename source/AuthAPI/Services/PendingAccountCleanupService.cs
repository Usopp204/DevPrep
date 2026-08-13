using AuthAPI.Models;
using AuthAPI.Repositories.Interfaces;
using AuthAPI.Services.Interfaces;

namespace AuthAPI.Services;

/// <summary>
/// Job nền dọn định kỳ các account "Pending" đã hết hạn xác thực email (không còn OTP/link
/// hợp lệ nào) — giải phóng email để đăng ký lại và giữ DB sạch. Đây là lưới quét bổ sung;
/// đường chính là lazy delete ngay trong RegisterAsync nên đăng ký lại không phụ thuộc job này.
/// Sau khi xoá account, gọi UserAPI xoá hồ sơ tương ứng (best-effort).
/// </summary>
public class PendingAccountCleanupService : BackgroundService
{
    private const int DefaultIntervalMinutes = 10;
    private const int DefaultStartupDelaySeconds = 15;
    private const int BatchSize = 100;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingAccountCleanupService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _startupDelay;

    public PendingAccountCleanupService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PendingAccountCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(
            configuration.GetValue("PENDING_ACCOUNT_CLEANUP_MINUTES", DefaultIntervalMinutes));
        _startupDelay = TimeSpan.FromSeconds(
            configuration.GetValue("PENDING_ACCOUNT_CLEANUP_STARTUP_DELAY_SECONDS", DefaultStartupDelaySeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(_startupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Lỗi 1 lượt quét (DB tắt...) không được giết job — chờ lượt sau.
                _logger.LogError(ex, "Pending-account cleanup failed; will retry next cycle.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var userApiClient = scope.ServiceProvider.GetRequiredService<IUserApiClient>();

        var candidates = await accounts.GetExpiredPendingAccountsAsync(BatchSize);
        if (candidates.Count == 0)
        {
            return;
        }

        // Xoá từng account bằng DELETE có điều kiện nguyên tử: account vừa được resend cứu sống
        // (có token mới) hoặc đã bị register/lượt khác xoá thì tự bỏ qua — và một account lỗi
        // không làm hỏng cả batch.
        var deleted = new List<Account>();
        foreach (var account in candidates)
        {
            if (await accounts.TryDeleteExpiredPendingAsync(account.AccountId))
            {
                deleted.Add(account);
            }
        }

        if (deleted.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "Cleaned up {Count} pending accounts whose verification window expired: {Emails}",
            deleted.Count, string.Join(", ", deleted.Select(a => a.Email)));

        // Xoá hồ sơ bên UserAPI (best-effort — lỗi chỉ log, lượt quét sau không lặp lại
        // vì account đã xoá; hồ sơ mồ côi nếu còn cũng vô hại).
        try
        {
            await userApiClient.DeleteProfilesAsync(
                deleted.Select(a => a.AccountId).ToList(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call UserAPI (delete profiles) for {Count} cleaned account(s).", deleted.Count);
        }
    }
}

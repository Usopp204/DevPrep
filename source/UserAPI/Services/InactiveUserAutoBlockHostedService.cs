using UserAPI.Helpers;
using UserAPI.Services.Interfaces;

namespace UserAPI.Services;

public class InactiveUserAutoBlockHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InactiveUserAutoBlockHostedService> _logger;

    private const int InactiveDays = 30;

    public InactiveUserAutoBlockHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<InactiveUserAutoBlockHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    //{
    //    while (!stoppingToken.IsCancellationRequested)
    //    {
    //        try
    //        {
    //            using var scope = _scopeFactory.CreateScope();

    //            var adminUserService = scope.ServiceProvider.GetRequiredService<IAdminUserService>();

    //            var result = await adminUserService.AutoBlockInactiveUsersAsync(InactiveDays);

    //            _logger.LogInformation(
    //                "Auto-block inactive users completed. InactiveDays={InactiveDays}, BlockedCount={BlockedCount}",
    //                result.InactiveDays,
    //                result.BlockedCount
    //            );
    //        }
    //        catch (OperationCanceledException)
    //        {
    //            // App is stopping.
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogError(ex, "Auto-block inactive users failed.");
    //        }

    //        await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
    //    }
    //}


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = VietnamTime.Now;
            var nextRun = now.Hour < 1
                ? now.Date.AddHours(1)
                : now.Date.AddDays(1).AddHours(1);
            var delay = nextRun - now;

            try
            {
                await Task.Delay(delay, stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var adminUserService = scope.ServiceProvider.GetRequiredService<IAdminUserService>();
                var result = await adminUserService.AutoBlockInactiveUsersAsync(InactiveDays);

                _logger.LogInformation(
                    "Auto-block inactive users completed. InactiveDays={InactiveDays}, BlockedCount={BlockedCount}",
                    result.InactiveDays,
                    result.BlockedCount
                );
            }
            catch (OperationCanceledException)
            {
                // App is stopping.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-block inactive users failed.");
            }
        }
    }
}
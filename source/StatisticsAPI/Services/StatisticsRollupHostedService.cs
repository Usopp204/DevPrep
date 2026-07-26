namespace StatisticsAPI.Services;

/// <summary>
/// Chạy rollup định kỳ: 1 lần lúc khởi động (chờ vài giây cho các service khác lên) + lặp lại mỗi N giờ.
/// Best-effort — service khác chưa chạy thì lần đó ghi warning; admin có thể bấm POST /rebuild để chạy tay.
/// </summary>
public sealed class StatisticsRollupHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<StatisticsRollupHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startupDelaySeconds = Math.Clamp(configuration.GetValue("Statistics:StartupDelaySeconds", 20), 0, 600);
        var intervalHours = Math.Clamp(configuration.GetValue("Statistics:RefreshIntervalHours", 6), 1, 168);

        try { await Task.Delay(TimeSpan.FromSeconds(startupDelaySeconds), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var collector = scope.ServiceProvider.GetRequiredService<IStatisticsCollector>();
                var result = await collector.RunAsync(stoppingToken);
                logger.LogInformation("Scheduled rollup: {Snapshots} snapshots, {Counters} counters, {Warnings} warnings",
                    result.SnapshotsWritten, result.CountersWritten, result.Warnings.Count);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled statistics rollup failed");
            }

            try { await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}

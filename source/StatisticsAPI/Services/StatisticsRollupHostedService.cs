namespace StatisticsAPI.Services;

public sealed class StatisticsRollupHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<StatisticsRollupHostedService> logger) : BackgroundService
{
    private const int RequiredRefreshIntervalMinutes = 15;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startupDelaySeconds = Math.Clamp(
            configuration.GetValue("Statistics:StartupDelaySeconds", 20),
            0,
            600);
        var configuredRefreshInterval = configuration.GetValue(
            "Statistics:RefreshIntervalMinutes",
            RequiredRefreshIntervalMinutes);
        if (configuredRefreshInterval != RequiredRefreshIntervalMinutes)
        {
            logger.LogWarning(
                "Statistics refresh interval {ConfiguredMinutes} was ignored; the approved interval is {RequiredMinutes} minutes.",
                configuredRefreshInterval,
                RequiredRefreshIntervalMinutes);
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(startupDelaySeconds), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var collector = scope.ServiceProvider.GetRequiredService<IStatisticsCollector>();
                var result = await collector.RunAsync(waitForAvailability: true, stoppingToken);
                logger.LogInformation(
                    "Scheduled dashboard rollup: {Snapshots} snapshots, {Counters} counters, success={Success}.",
                    result.SnapshotsWritten,
                    result.CountersWritten,
                    result.Ok);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled dashboard statistics rollup failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(RequiredRefreshIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
using Services.POI.Database;

namespace Services.POI;

/// <summary>
/// Forces the Overpass store sync every 24h for the lifetime of the process. The startup sync in
/// Program.cs (and a manual POST /stores/update) already respect OverpassDataFetcher's own 24h
/// freshness check, but nothing re-triggers that check while the process keeps running - without
/// this, a long-lived process's store data goes stale forever after the first sync.
/// </summary>
internal sealed class OverpassSyncBackgroundService(
    IServiceScopeFactory scopeFactory, ILogger<OverpassSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Doesn't fire immediately - Program.cs already ran a sync at startup, so the first forced
        // refresh here naturally lands ~24h after that one.
        using PeriodicTimer timer = new(TimeSpan.FromHours(24));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                Context ctx = scope.ServiceProvider.GetRequiredService<Context>();
                await OverpassDataFetcher.LoadStores(ctx, logger, stoppingToken, force: true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed tick shouldn't stop future 24h refreshes from being attempted.
                logger.LogError(ex, "Scheduled Overpass store sync failed");
            }
        }
    }
}

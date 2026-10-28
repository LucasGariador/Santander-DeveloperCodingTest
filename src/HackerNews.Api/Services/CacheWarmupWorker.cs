using HackerNews.Api.Configuration;
using Microsoft.Extensions.Options;

namespace HackerNews.Api.Services;

public class CacheWarmupWorker(
    IStoriesService storiesService,
    IOptions<HackerNewsOptions> options,
    ILogger<CacheWarmupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting background cache warm-up for Hacker News stories...");

        await SafeRefreshAsync(stoppingToken);

        var refreshInterval = TimeSpan.FromMinutes(Math.Max(1, options.Value.IdsCacheDurationMinutes));
        using var timer = new PeriodicTimer(refreshInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SafeRefreshAsync(stoppingToken);
        }
    }

    private async Task SafeRefreshAsync(CancellationToken ct)
    {
        try
        {
            await storiesService.RefreshCacheAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while warming up Hacker News cache.");
        }
    }
}
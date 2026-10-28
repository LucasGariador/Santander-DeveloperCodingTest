using HackerNews.Api.Clients;
using HackerNews.Api.Configuration;
using HackerNews.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HackerNews.Api.Services;

public interface IStoriesService
{
    Task<IEnumerable<StoryDto>> GetBestStoriesAsync(int count, CancellationToken ct = default);
    Task RefreshCacheAsync(CancellationToken ct = default);
}

public class StoriesService : IStoriesService
{
    private const string AllSortedStoriesCacheKey = "BestStories_AllSorted";
    private readonly IHackerNewsClient _hnClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StoriesService> _logger;
    private readonly HackerNewsOptions _options;

    private static readonly SemaphoreSlim RefreshLock = new(1, 1);
    private readonly SemaphoreSlim _httpThrottle;

    public StoriesService(
        IHackerNewsClient hnClient,
        IMemoryCache cache,
        IOptions<HackerNewsOptions> options,
        ILogger<StoriesService> logger)
    {
        _hnClient = hnClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
        _httpThrottle = new SemaphoreSlim(_options.MaxConcurrentRequests, _options.MaxConcurrentRequests);
    }

    public async Task<IEnumerable<StoryDto>> GetBestStoriesAsync(int count, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(AllSortedStoriesCacheKey, out List<StoryDto>? cachedList) && cachedList is not null)
        {
            return cachedList.Take(count);
        }

        await RefreshLock.WaitAsync(ct);
        try
        {
            // Double-check lock pattern to prevent cache stampede when multiple requests wait on the semaphore
            if (_cache.TryGetValue(AllSortedStoriesCacheKey, out cachedList) && cachedList is not null)
            {
                return cachedList.Take(count);
            }

            var sortedStories = await FetchAndSortAllBestStoriesAsync(ct);
            _cache.Set(AllSortedStoriesCacheKey, sortedStories, TimeSpan.FromMinutes(_options.IdsCacheDurationMinutes));

            return sortedStories.Take(count);
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    public async Task RefreshCacheAsync(CancellationToken ct = default)
    {
        await RefreshLock.WaitAsync(ct);
        try
        {
            var sortedStories = await FetchAndSortAllBestStoriesAsync(ct);
            if (sortedStories.Count > 0)
            {
                _cache.Set(AllSortedStoriesCacheKey, sortedStories, TimeSpan.FromMinutes(_options.IdsCacheDurationMinutes));
                _logger.LogInformation("Hacker News cache warmed up successfully with {Count} stories.", sortedStories.Count);
            }
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private async Task<List<StoryDto>> FetchAndSortAllBestStoriesAsync(CancellationToken ct)
    {
        var storyIds = await _hnClient.GetBestStoryIdsAsync(ct);
        if (storyIds.Count == 0)
            return [];

        var tasks = storyIds.Select(id => GetStoryDetailCachedAsync(id, ct));
        var stories = await Task.WhenAll(tasks);

        return stories
            .Where(s => s is not null)
            .Select(s => s!)
            .OrderByDescending(s => s.Score)
            .ToList();
    }

    private async Task<StoryDto?> GetStoryDetailCachedAsync(int id, CancellationToken ct)
    {
        string itemCacheKey = $"Story_{id}";
        if (_cache.TryGetValue(itemCacheKey, out StoryDto? cachedStory))
            return cachedStory;

        await _httpThrottle.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(itemCacheKey, out cachedStory))
                return cachedStory;

            var item = await _hnClient.GetStoryItemAsync(id, ct);
            if (item is null) return null;

            var dto = new StoryDto(
                Title: item.Title ?? string.Empty,
                Uri: item.Url ?? string.Empty,
                PostedBy: item.By ?? string.Empty,
                Time: DateTimeOffset.FromUnixTimeSeconds(item.Time),
                Score: item.Score,
                CommentCount: item.Descendants
            );

            _cache.Set(itemCacheKey, dto, TimeSpan.FromMinutes(_options.ItemCacheDurationMinutes));
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve story details for ID {StoryId}", id);
            return null;
        }
        finally
        {
            _httpThrottle.Release();
        }
    }
}
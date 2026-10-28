using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using HackerNews.Api.Clients;
using HackerNews.Api.Configuration;
using HackerNews.Api.Models;

namespace HackerNews.Api.Services;

public interface IStoriesService
{
    Task<IEnumerable<StoryDto>> GetBestStoriesAsync(int count, CancellationToken ct = default);
}

public class StoriesService : IStoriesService
{
    private const string BestStoriesCacheKey = "BestStories_Ids";
    private readonly IHackerNewsClient _hnClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StoriesService> _logger;
    private readonly HackerNewsOptions _options;
    
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
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
        var storyIds = await GetBestStoryIdsCachedAsync(ct);
        if (storyIds.Count == 0)
            return [];

        var tasks = storyIds.Select(id => GetStoryDetailCachedAsync(id, ct));
        var stories = await Task.WhenAll(tasks);

        return stories
            .Where(s => s is not null)
            .Select(s => s!)
            .OrderByDescending(s => s.Score)
            .Take(count);
    }

    private async Task<IReadOnlyList<int>> GetBestStoryIdsCachedAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(BestStoriesCacheKey, out IReadOnlyList<int>? cachedIds) && cachedIds is not null)
            return cachedIds;

        await CacheLock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(BestStoriesCacheKey, out cachedIds) && cachedIds is not null)
                return cachedIds;

            var ids = await _hnClient.GetBestStoryIdsAsync(ct);
            _cache.Set(BestStoriesCacheKey, ids, TimeSpan.FromMinutes(_options.IdsCacheDurationMinutes));
            return ids;
        }
        finally
        {
            CacheLock.Release();
        }
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
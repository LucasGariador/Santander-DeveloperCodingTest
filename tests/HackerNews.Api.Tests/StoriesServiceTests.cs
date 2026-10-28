using FluentAssertions;
using HackerNews.Api.Clients;
using HackerNews.Api.Configuration;
using HackerNews.Api.Models;
using HackerNews.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HackerNews.Api.Tests;

public class StoriesServiceTests
{
    private readonly Mock<IHackerNewsClient> _mockClient;
    private readonly IMemoryCache _memoryCache;
    private readonly IOptions<HackerNewsOptions> _options;

    public StoriesServiceTests()
    {
        _mockClient = new Mock<IHackerNewsClient>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _options = Options.Create(new HackerNewsOptions
        {
            IdsCacheDurationMinutes = 5,
            ItemCacheDurationMinutes = 5,
            MaxConcurrentRequests = 10
        });
    }

    private StoriesService CreateService() =>
        new(_mockClient.Object, _memoryCache, _options, NullLogger<StoriesService>.Instance);

    [Fact]
    public async Task GetBestStoriesAsync_ShouldReturnStoriesOrderedByScoreDescending()
    {
        _mockClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync([1, 2, 3]);

        _mockClient.Setup(c => c.GetStoryItemAsync(1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new HackerNewsItem(1, "Low Score", "http://a.com", "user1", 1700000000, 50, 5));
        _mockClient.Setup(c => c.GetStoryItemAsync(2, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new HackerNewsItem(2, "High Score", "http://b.com", "user2", 1700000000, 500, 40));
        _mockClient.Setup(c => c.GetStoryItemAsync(3, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new HackerNewsItem(3, "Mid Score", "http://c.com", "user3", 1700000000, 150, 12));

        var sut = CreateService();

        var result = (await sut.GetBestStoriesAsync(2)).ToList();

        result.Should().HaveCount(2);
        result[0].Title.Should().Be("High Score");
        result[0].Score.Should().Be(500);
        result[1].Title.Should().Be("Mid Score");
        result[1].Score.Should().Be(150);
    }

    [Fact]
    public async Task GetBestStoriesAsync_SecondCall_ShouldReturnFromCacheWithoutCallingClientAgain()
    {
        _mockClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync([10]);

        _mockClient.Setup(c => c.GetStoryItemAsync(10, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new HackerNewsItem(10, "Cached Story", "http://cache.com", "author", 1700000000, 100, 10));

        var sut = CreateService();

        var firstCall = await sut.GetBestStoriesAsync(1);
        var secondCall = await sut.GetBestStoriesAsync(1);

        firstCall.Should().HaveCount(1);
        secondCall.Should().HaveCount(1);

        _mockClient.Verify(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockClient.Verify(c => c.GetStoryItemAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBestStoriesAsync_WhenOneItemFailsOrReturnsNull_ShouldStillReturnRemainingStories()
    {
        _mockClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync([1, 2]);

        _mockClient.Setup(c => c.GetStoryItemAsync(1, It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new HttpRequestException("Simulated network timeout"));
        _mockClient.Setup(c => c.GetStoryItemAsync(2, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new HackerNewsItem(2, "Surviving Story", null, "user2", 1700000000, 300, 25));

        var sut = CreateService();


        var result = (await sut.GetBestStoriesAsync(2)).ToList();

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Surviving Story");
        result[0].Uri.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshCacheAsync_ShouldPrepopulateCacheForSubsequentReads()
    {
        _mockClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync([99]);
        _mockClient.Setup(c => c.GetStoryItemAsync(99, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new HackerNewsItem(99, "Warmed Story", "http://warm.com", "user99", 1700000000, 999, 88));

        var sut = CreateService();

        await sut.RefreshCacheAsync();
        var result = (await sut.GetBestStoriesAsync(1)).ToList();

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Warmed Story");
        _mockClient.Verify(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
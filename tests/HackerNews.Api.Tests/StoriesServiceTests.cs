using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HackerNews.Api.Clients;
using HackerNews.Api.Configuration;
using HackerNews.Api.Models;
using HackerNews.Api.Services;

namespace HackerNews.Api.Tests;

public class StoriesServiceTests
{
    [Fact]
    public async Task GetBestStoriesAsync_ShouldReturnRequestedAmount_OrderedByScoreDescending()
    {
        var mockClient = new Mock<IHackerNewsClient>();
        var realCache = new MemoryCache(new MemoryCacheOptions());
        var mockOptions = Options.Create(new HackerNewsOptions { 
            IdsCacheDurationMinutes = 5, 
            ItemCacheDurationMinutes = 5, 
            MaxConcurrentRequests = 10 
        });
        var mockLogger = new Mock<ILogger<StoriesService>>();

        mockClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new List<int> { 1, 2, 3 });

        mockClient.Setup(c => c.GetStoryItemAsync(1, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new HackerNewsItem(1, "Title 1", "url1", "user1", 12345, 100, 10));
        mockClient.Setup(c => c.GetStoryItemAsync(2, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new HackerNewsItem(2, "Title 2", "url2", "user2", 12345, 300, 20));
        mockClient.Setup(c => c.GetStoryItemAsync(3, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new HackerNewsItem(3, "Title 3", "url3", "user3", 12345, 200, 30));

        var service = new StoriesService(mockClient.Object, realCache, mockOptions, mockLogger.Object);

        var result = await service.GetBestStoriesAsync(2);

        var resultList = result.ToList();
        resultList.Should().HaveCount(2);
        
        resultList[0].Title.Should().Be("Title 2");
        resultList[0].Score.Should().Be(300);
        
        resultList[1].Title.Should().Be("Title 3");
        resultList[1].Score.Should().Be(200);
    }
}
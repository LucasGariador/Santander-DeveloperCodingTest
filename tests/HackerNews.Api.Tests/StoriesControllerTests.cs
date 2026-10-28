using FluentAssertions;
using HackerNews.Api.Controllers;
using HackerNews.Api.Models;
using HackerNews.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace HackerNews.Api.Tests;

public class StoriesControllerTests
{
    [Fact]
    public async Task GetBestStories_WithValidCount_ReturnsOkResultWithStories()
    {
        var mockService = new Mock<IStoriesService>();
        var fakeStories = new List<StoryDto>
        {
            new("Title 1", "uri1", "user1", DateTimeOffset.UtcNow, 200, 15),
            new("Title 2", "uri2", "user2", DateTimeOffset.UtcNow, 150, 10)
        };

        mockService.Setup(s => s.GetBestStoriesAsync(2, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(fakeStories);

        var controller = new StoriesController(mockService.Object);

        var result = await controller.GetBestStories(count: 2, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedStories = okResult.Value.Should().BeAssignableTo<IEnumerable<StoryDto>>().Subject;

        returnedStories.Should().HaveCount(2);
        returnedStories.First().Title.Should().Be("Title 1");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(201)]
    public async Task GetBestStories_WithInvalidCount_ReturnsBadRequest(int invalidCount)
    {
        var mockService = new Mock<IStoriesService>();
        var controller = new StoriesController(mockService.Object);

        var result = await controller.GetBestStories(count: invalidCount, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        mockService.Verify(s => s.GetBestStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
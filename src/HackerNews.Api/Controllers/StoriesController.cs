using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using HackerNews.Api.Models;
using HackerNews.Api.Services;

namespace HackerNews.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StoriesController(IStoriesService storiesService) : ControllerBase
{
    /// <summary>
    /// Retrieves the best N stories from Hacker News ordered by score descending.
    /// </summary>
    /// <param name="count">The number of stories to retrieve (between 1 and 200).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("best")]
    [ProducesResponseType(typeof(IEnumerable<StoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetBestStories(
        [FromQuery(Name = "n"), Range(1, 200, ErrorMessage = "Parameter 'n' must be between 1 and 200.")] int count = 10,
        CancellationToken cancellationToken = default)
    {
        var stories = await storiesService.GetBestStoriesAsync(count, cancellationToken);
        return Ok(stories);
    }
}
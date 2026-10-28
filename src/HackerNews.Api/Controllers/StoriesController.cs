using HackerNews.Api.Models;
using HackerNews.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HackerNews.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoriesController(IStoriesService storiesService) : ControllerBase
{
    [HttpGet("best")]
    [ProducesResponseType(typeof(IEnumerable<StoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetBestStories(
        [FromQuery(Name = "n")] int count = 10,
        CancellationToken ct = default)
    {
        if (count is < 1 or > 200)
        {
            return BadRequest("The parameter 'n' must be between 1 and 200.");
        }

        var stories = await storiesService.GetBestStoriesAsync(count, ct);
        return Ok(stories);
    }
}
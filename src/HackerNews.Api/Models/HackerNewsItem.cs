using System.Text.Json.Serialization;

namespace HackerNews.Api.Models;

public record HackerNewsItem(
    int Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("by")] string? By,
    [property: JsonPropertyName("time")] long Time,
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("descendants")] int Descendants
);
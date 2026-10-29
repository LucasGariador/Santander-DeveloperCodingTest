using System.Text.Json.Serialization;

namespace HackerNews.Api.Models;

public record StoryDto(
    string Title,
    string Uri,
    string PostedBy,
    DateTimeOffset Time,
    int Score,
    int CommentCount
);
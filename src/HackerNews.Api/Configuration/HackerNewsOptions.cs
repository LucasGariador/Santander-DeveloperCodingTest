namespace HackerNews.Api.Configuration;

public class HackerNewsOptions
{
    public const string SectionName = "HackerNews";

    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0/";
    public int IdsCacheDurationMinutes { get; set; } = 2;
    public int ItemCacheDurationMinutes { get; set; } = 5;
    public int MaxConcurrentRequests { get; set; } = 25;
} 
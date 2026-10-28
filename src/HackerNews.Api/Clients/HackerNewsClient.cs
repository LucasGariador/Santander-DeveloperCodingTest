using HackerNews.Api.Models;

namespace HackerNews.Api.Clients;

public interface IHackerNewsClient
{
    Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct = default);
    Task<HackerNewsItem?> GetStoryItemAsync(int id, CancellationToken ct = default);
}

public class HackerNewsClient(HttpClient httpClient) : IHackerNewsClient
{
    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct = default)
    {
        var ids = await httpClient.GetFromJsonAsync<List<int>>("beststories.json", ct);
        return ids ?? [];
    }

    public async Task<HackerNewsItem?> GetStoryItemAsync(int id, CancellationToken ct = default)
    {
        return await httpClient.GetFromJsonAsync<HackerNewsItem>($"item/{id}.json", ct);
    }
}
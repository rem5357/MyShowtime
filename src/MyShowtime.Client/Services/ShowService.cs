using System.Net.Http.Json;
using MyShowtime.Shared.Dtos;
using MyShowtime.Shared.Requests;

namespace MyShowtime.Client.Services;

public class ShowService(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<IReadOnlyList<ShowDto>> GetShowsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/shows", cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<List<ShowDto>>(cancellationToken: cancellationToken);
        return payload ?? [];
    }

    public async Task<IReadOnlyList<TmdbSearchResultDto>> SearchTmdbAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Array.Empty<TmdbSearchResultDto>();
        }

        var response = await _httpClient.GetAsync($"api/search?query={Uri.EscapeDataString(searchTerm.Trim())}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<TmdbSearchResultDto>();
        }

        var payload = await response.Content.ReadFromJsonAsync<List<TmdbSearchResultDto>>(cancellationToken: cancellationToken);
        return payload ?? [];
    }

    public async Task<ShowDto?> SaveShowAsync(SaveShowRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/shows", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShowDto>(cancellationToken: cancellationToken);
    }

    public async Task DeleteShowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/shows/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ShowDto?> RefreshShowAsync(int tmdbId, string? mediaType = null, CancellationToken cancellationToken = default)
    {
        var requestUri = $"api/shows/{tmdbId}/refresh";
        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            requestUri += $"?mediaType={Uri.EscapeDataString(mediaType)}";
        }

        var response = await _httpClient.PostAsync(requestUri, null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ShowDto>(cancellationToken: cancellationToken);
    }
}

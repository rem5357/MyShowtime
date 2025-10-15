using System.Net.Http.Json;
using MyShowtime.Shared.Dtos;
using MyShowtime.Shared.Enums;
using MyShowtime.Shared.Requests;

namespace MyShowtime.Client.Services;

public class MediaLibraryService(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<IReadOnlyList<MediaSummaryDto>> GetMediaAsync(bool includeHidden = false, CancellationToken cancellationToken = default)
    {
        var query = includeHidden ? "?includeHidden=true" : string.Empty;
        var response = await _httpClient.GetAsync($"api/media{query}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<List<MediaSummaryDto>>(cancellationToken: cancellationToken);
        return payload ?? new List<MediaSummaryDto>();
    }

    public async Task<MediaDetailDto?> GetMediaDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/media/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<MediaDetailDto>(cancellationToken: cancellationToken)
            : null;
    }

    public async Task<IReadOnlyList<EpisodeDto>> GetEpisodesAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/media/{mediaId}/episodes", cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<List<EpisodeDto>>(cancellationToken: cancellationToken);
        return payload ?? new List<EpisodeDto>();
    }

    public async Task<MediaDetailDto?> ImportMediaAsync(ImportMediaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/media/import", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MediaDetailDto>(cancellationToken: cancellationToken);
    }

    public async Task<MediaDetailDto?> SyncMediaAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/media/{mediaId}/sync", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MediaDetailDto>(cancellationToken: cancellationToken);
    }

    public async Task<MediaDetailDto?> UpdateMediaAsync(Guid mediaId, UpdateMediaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/media/{mediaId}", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MediaDetailDto>(cancellationToken: cancellationToken);
    }

    public async Task<EpisodeDto?> UpdateEpisodeViewStateAsync(Guid mediaId, Guid episodeId, ViewState state, CancellationToken cancellationToken = default)
    {
        var payload = new UpdateEpisodeViewStateRequest { WatchState = state };
        var response = await _httpClient.PutAsJsonAsync($"api/media/{mediaId}/episodes/{episodeId}/viewstate", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<EpisodeDto>(cancellationToken: cancellationToken);
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
        return payload ?? new List<TmdbSearchResultDto>();
    }

    public async Task<MediaDetailDto?> GetTmdbPreviewAsync(int tmdbId, MediaType mediaType, CancellationToken cancellationToken = default)
    {
        var url = $"api/search/preview?tmdbId={tmdbId}&mediaType={Uri.EscapeDataString(mediaType.ToString())}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MediaDetailDto>(cancellationToken: cancellationToken);
    }
}

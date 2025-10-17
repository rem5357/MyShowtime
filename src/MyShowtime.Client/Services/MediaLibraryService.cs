using System.Net;
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

    public async Task<bool> BulkUpdateEpisodesAsync(Guid mediaId, ViewState state, bool excludeSpecials, CancellationToken cancellationToken = default)
    {
        var payload = new BulkUpdateEpisodesRequest
        {
            WatchState = state,
            ExcludeSpecials = excludeSpecials
        };
        var response = await _httpClient.PutAsJsonAsync($"api/media/{mediaId}/episodes/bulk", payload, cancellationToken);
        return response.IsSuccessStatusCode;
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

    public async Task<TmdbSearchResponseDto> SearchTmdbAsync(string? searchTerm, string? mediaType = null, int page = 1, CancellationToken cancellationToken = default)
    {
        var normalizedPage = page <= 0 ? 1 : page;
        var queryParameters = new List<string> { $"page={normalizedPage}" };

        var sanitizedType = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType.Trim();
        if (!string.IsNullOrWhiteSpace(sanitizedType))
        {
            queryParameters.Add($"type={Uri.EscapeDataString(sanitizedType)}");
        }

        var trimmedQuery = searchTerm?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedQuery))
        {
            queryParameters.Add($"q={Uri.EscapeDataString(trimmedQuery)}");
        }

        var path = queryParameters.Count > 0
            ? $"api/tmdb/search?{string.Join("&", queryParameters)}"
            : "api/tmdb/search";

        var response = await _httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return TmdbSearchResponseDtoExtensions.Empty;
        }
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TmdbSearchResponseDto>(cancellationToken: cancellationToken);
        return payload ?? TmdbSearchResponseDtoExtensions.Empty;
    }

    public async Task<MediaDetailDto?> GetTmdbPreviewAsync(int tmdbId, string mediaType, CancellationToken cancellationToken = default)
    {
        if (tmdbId <= 0)
        {
            return null;
        }

        var url = $"api/tmdb/details?tmdbId={tmdbId}&mediaType={Uri.EscapeDataString(mediaType)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return null;
            }
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MediaDetailDto>(cancellationToken: cancellationToken);
    }
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Options;
using MyShowtime.Api.Options;
using MyShowtime.Api.Services.Models;
using MyShowtime.Shared.Dtos;
using MyShowtime.Shared.Enums;

namespace MyShowtime.Api.Services;

public class TmdbClient(HttpClient httpClient, IOptions<TmdbOptions> options) : ITmdbClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = httpClient;
    private readonly TmdbOptions _options = options.Value;

    public async Task<IReadOnlyList<TmdbSearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<TmdbSearchResultDto>();
        }

        using var request = BuildRequest(HttpMethod.Get, $"search/multi?query={Uri.EscapeDataString(query.Trim())}&include_adult=false");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("results", out var results))
        {
            return Array.Empty<TmdbSearchResultDto>();
        }

        var list = new List<TmdbSearchResultDto>(results.GetArrayLength());
        foreach (var element in results.EnumerateArray())
        {
            var typeValue = element.GetPropertyOrDefault("media_type")?.GetString();
            if (typeValue is null || (typeValue != "movie" && typeValue != "tv"))
            {
                continue;
            }

            var mediaType = typeValue == "movie" ? MediaType.Movie : MediaType.TvShow;
            var id = element.GetPropertyOrDefault("id")?.GetInt32() ?? 0;
            if (id == 0)
            {
                continue;
            }

            var title = mediaType == MediaType.TvShow
                ? element.GetPropertyOrDefault("name")?.GetString()
                : element.GetPropertyOrDefault("title")?.GetString();

            if (string.IsNullOrWhiteSpace(title))
            {
                title = element.GetPropertyOrDefault("original_title")?.GetString()
                    ?? element.GetPropertyOrDefault("original_name")?.GetString()
                    ?? "Untitled";
            }

            var overview = element.GetPropertyOrDefault("overview")?.GetString();
            var posterPath = element.GetPropertyOrDefault("poster_path")?.GetString();
            var releaseDateProperty = mediaType == MediaType.TvShow ? "first_air_date" : "release_date";
            var releaseDate = element.GetPropertyOrDefault(releaseDateProperty)?.GetString();

            list.Add(new TmdbSearchResultDto(
                id,
                mediaType,
                title!,
                overview,
                posterPath,
                releaseDate,
                null));
        }

        if (list.Count == 0)
        {
            return list;
        }

        var providerTasks = list.Select(result =>
            GetWatchProvidersAsync(result.TmdbId, result.MediaType, cancellationToken)).ToArray();

        try
        {
            await Task.WhenAll(providerTasks);
        }
        catch
        {
            // Ignore provider failures; we'll fall back to null sources.
        }

        var enriched = new List<TmdbSearchResultDto>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var result = list[i];
            var providers = providerTasks[i].Status == TaskStatus.RanToCompletion
                ? providerTasks[i].Result
                : null;
            var source = SelectPrimaryProvider(providers);
            enriched.Add(result with { AvailableOn = source });
        }

        return enriched;
    }

    public async Task<TmdbMovieDetails?> GetMovieDetailsAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        if (tmdbId <= 0)
        {
            return null;
        }

        var path = $"movie/{tmdbId}?append_to_response=credits,watch/providers";
        using var request = BuildRequest(HttpMethod.Get, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<TmdbMovieDetails>(stream, SerializerOptions, cancellationToken);
    }

    public async Task<TmdbTvDetails?> GetTvDetailsAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        if (tmdbId <= 0)
        {
            return null;
        }

        var path = $"tv/{tmdbId}?append_to_response=aggregate_credits,credits,watch/providers";
        using var request = BuildRequest(HttpMethod.Get, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<TmdbTvDetails>(stream, SerializerOptions, cancellationToken);
    }

    public async Task<TmdbSeasonDetails?> GetTvSeasonAsync(int tmdbId, int seasonNumber, CancellationToken cancellationToken = default)
    {
        if (tmdbId <= 0 || seasonNumber < 0)
        {
            return null;
        }

        var path = $"tv/{tmdbId}/season/{seasonNumber}";
        using var request = BuildRequest(HttpMethod.Get, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<TmdbSeasonDetails>(stream, SerializerOptions, cancellationToken);
    }

    public async Task<TmdbWatchProviders?> GetWatchProvidersAsync(int tmdbId, MediaType mediaType, CancellationToken cancellationToken = default)
    {
        if (tmdbId <= 0)
        {
            return null;
        }

        var path = mediaType == MediaType.Movie
            ? $"movie/{tmdbId}/watch/providers"
            : $"tv/{tmdbId}/watch/providers";

        using var request = BuildRequest(HttpMethod.Get, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<TmdbWatchProviders>(stream, SerializerOptions, cancellationToken);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("TMDB API key is not configured. Set the Tmdb:ApiKey configuration value.");
        }

        var uriBuilder = new StringBuilder(relativeUrl);
        _ = uriBuilder.ToString().Contains('?')
            ? uriBuilder.Append("&api_key=").Append(Uri.EscapeDataString(_options.ApiKey))
            : uriBuilder.Append("?api_key=").Append(Uri.EscapeDataString(_options.ApiKey));

        var request = new HttpRequestMessage(method, uriBuilder.ToString());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static string? SelectPrimaryProvider(TmdbWatchProviders? providers)
    {
        if (providers?.Results is null || providers.Results.Count == 0)
        {
            return null;
        }

        var priorityCountries = new[] { "US", "CA", "GB", "AU" };
        foreach (var country in priorityCountries)
        {
            if (!providers.Results.TryGetValue(country, out var entry))
            {
                continue;
            }

            var provider = entry.Flatrate?.FirstOrDefault()
                          ?? entry.Ads?.FirstOrDefault()
                          ?? entry.Rent?.FirstOrDefault()
                          ?? entry.Buy?.FirstOrDefault();
            if (provider is not null)
            {
                return provider.ProviderName;
            }
        }

        var first = providers.Results.Values
            .SelectMany(v => (v.Flatrate ?? Array.Empty<TmdbWatchProviderEntry>())
                .Concat(v.Ads ?? Array.Empty<TmdbWatchProviderEntry>())
                .Concat(v.Rent ?? Array.Empty<TmdbWatchProviderEntry>())
                .Concat(v.Buy ?? Array.Empty<TmdbWatchProviderEntry>()))
            .FirstOrDefault();

        return first?.ProviderName;
    }
}

internal static class JsonExtensions
{
    public static JsonElement? GetPropertyOrDefault(this JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) ? value : null;
}

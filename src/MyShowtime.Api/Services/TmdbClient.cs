using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MyShowtime.Api.Options;
using MyShowtime.Shared.Dtos;

namespace MyShowtime.Api.Services;

public class TmdbClient(HttpClient httpClient, IOptions<TmdbOptions> options) : ITmdbClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly TmdbOptions _options = options.Value;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<TmdbSearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<TmdbSearchResultDto>();
        }

        using var request = BuildRequest(HttpMethod.Get, $"search/multi?query={Uri.EscapeDataString(query.Trim())}");
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
            var id = element.GetPropertyOrDefault("id")?.GetInt32() ?? 0;
            if (id == 0)
            {
                continue;
            }

            var mediaType = element.GetPropertyOrDefault("media_type")?.GetString() ?? "movie";
            var title = mediaType == "tv"
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
            var releaseDate = element.GetPropertyOrDefault(mediaType == "tv" ? "first_air_date" : "release_date")?.GetString();

            list.Add(new TmdbSearchResultDto(
                id,
                mediaType ?? "movie",
                title!,
                overview,
                posterPath,
                releaseDate));
        }

        return list;
    }

    public async Task<TmdbDetailsDto?> GetDetailsAsync(int tmdbId, string mediaType, CancellationToken cancellationToken = default)
    {
        if (tmdbId <= 0)
        {
            return null;
        }

        var sanitizedType = string.IsNullOrWhiteSpace(mediaType) ? "movie" : mediaType.ToLowerInvariant();
        if (sanitizedType is not ("movie" or "tv"))
        {
            sanitizedType = "movie";
        }

        using var request = BuildRequest(HttpMethod.Get, $"{sanitizedType}/{tmdbId}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<TmdbDetailsDto>(stream, _serializerOptions, cancellationToken);
        return payload;
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
}

internal static class JsonExtensions
{
    public static JsonElement? GetPropertyOrDefault(this JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) ? value : null;
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Options;
using MyShowtime.Api.Options;
using MyShowtime.Api.Services.Models;

namespace MyShowtime.Api.Services;

public class TmdbClient(HttpClient httpClient, IOptions<TmdbOptions> options) : ITmdbClient
{
    private const int AggregatedPageSize = 200;
    private const int TmdbPageSize = 20;
    private const int PagesPerBatch = AggregatedPageSize / TmdbPageSize;
    private const int MaxPeopleResults = 10;
    private const int PersonPagesPerBatch = 5;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = httpClient;
    private readonly TmdbOptions _options = options.Value;
    private readonly SemaphoreSlim _configurationLock = new(1, 1);
    private TmdbConfiguration? _configuration;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
        => await EnsureConfigurationAsync(cancellationToken);

    public async Task<TmdbSearchResponse> SearchAsync(string query, string mediaType, int page, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return CreateEmptySearchResponse();
        }

        var sanitizedPage = page <= 0 ? 1 : page;
        var searchType = NormalizeSearchType(mediaType);
        var trimmedQuery = query.Trim();

        var aggregatedResults = new List<TmdbSearchResult>();
        var totalResults = 0;
        var totalTmdbPages = 0;
        var hasResponse = false;

        var startPage = ((sanitizedPage - 1) * PagesPerBatch) + 1;

        for (var currentPage = startPage; currentPage < startPage + PagesPerBatch; currentPage++)
        {
            if (totalTmdbPages > 0 && currentPage > totalTmdbPages)
            {
                break;
            }

            var response = await FetchSearchPageAsync(trimmedQuery, searchType, currentPage, cancellationToken);
            hasResponse = true;

            if (totalTmdbPages == 0)
            {
                totalTmdbPages = response.TotalPages;
                totalResults = response.TotalResults;
            }

            if (response.Results.Count > 0)
            {
                aggregatedResults.AddRange(response.Results);
            }

            if (aggregatedResults.Count >= AggregatedPageSize || currentPage >= response.TotalPages)
            {
                break;
            }
        }

        if (!hasResponse)
        {
            return CreateEmptySearchResponse();
        }

        if (totalResults == 0 && aggregatedResults.Count > 0)
        {
            totalResults = aggregatedResults.Count;
        }

        var normalizedTotalPages = totalResults > 0
            ? Math.Max(1, (int)Math.Ceiling(totalResults / (double)AggregatedPageSize))
            : Math.Max(1, (int)Math.Ceiling(totalTmdbPages / (double)PagesPerBatch));

        var trimmedResults = aggregatedResults.Take(AggregatedPageSize).ToList();

        return new TmdbSearchResponse
        {
            Page = sanitizedPage,
            TotalPages = normalizedTotalPages,
            TotalResults = totalResults,
            Results = trimmedResults
        };
    }

    public async Task<IReadOnlyList<TmdbPersonResult>> SearchPeopleAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<TmdbPersonResult>();
        }

        var trimmed = query.Trim();
        var aggregated = new List<TmdbPersonResult>();
        var totalPages = 0;

        for (var pageIndex = 1; pageIndex <= PersonPagesPerBatch; pageIndex++)
        {
            if (totalPages > 0 && pageIndex > totalPages)
            {
                break;
            }

            var response = await FetchPersonSearchPageAsync(trimmed, pageIndex, cancellationToken);
            if (response.Results.Count == 0)
            {
                break;
            }

            aggregated.AddRange(response.Results);
            totalPages = response.TotalPages;

            if (aggregated.Count >= MaxPeopleResults)
            {
                break;
            }
        }

        return aggregated.Count > MaxPeopleResults
            ? aggregated.Take(MaxPeopleResults).ToList()
            : aggregated;
    }

    public async Task<TmdbPersonMovieCredits?> GetPersonMovieCreditsAsync(int personId, CancellationToken cancellationToken = default)
    {
        if (personId <= 0)
        {
            return null;
        }

        var url = new StringBuilder($"person/{personId}/movie_credits")
            .Append("?language=").Append(Uri.EscapeDataString(_options.DefaultLanguage))
            .ToString();

        using var request = BuildRequest(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<TmdbPersonMovieCredits>(stream, SerializerOptions, cancellationToken);
    }

    private async Task<TmdbPersonSearchResponse> FetchPersonSearchPageAsync(string query, int page, CancellationToken cancellationToken)
    {
        var url = new StringBuilder("search/person")
            .Append("?query=").Append(Uri.EscapeDataString(query))
            .Append("&page=").Append(page)
            .Append("&include_adult=false")
            .Append("&language=").Append(Uri.EscapeDataString(_options.DefaultLanguage))
            .ToString();

        using var request = BuildRequest(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<TmdbPersonSearchResponse>(stream, SerializerOptions, cancellationToken);
        return payload ?? new TmdbPersonSearchResponse();
    }

    private async Task<TmdbSearchResponse> FetchSearchPageAsync(string query, string searchType, int page, CancellationToken cancellationToken)
    {
        var endpoint = searchType == "multi" ? "search/multi" : $"search/{searchType}";

        var url = new StringBuilder(endpoint)
            .Append("?query=").Append(Uri.EscapeDataString(query))
            .Append("&page=").Append(page)
            .Append("&include_adult=false")
            .Append("&language=").Append(Uri.EscapeDataString(_options.DefaultLanguage))
            .Append("&region=").Append(Uri.EscapeDataString(_options.DefaultRegion))
            .ToString();

        using var request = BuildRequest(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<TmdbSearchResponse>(stream, SerializerOptions, cancellationToken);
        return payload ?? CreateEmptySearchResponse();
    }

    public async Task<TmdbSearchResponse> GetTrendingAsync(int page, CancellationToken cancellationToken = default)
    {
        var sanitizedPage = page <= 0 ? 1 : page;
        var path = new StringBuilder("trending/all/week")
            .Append("?page=").Append(sanitizedPage)
            .Append("&language=").Append(Uri.EscapeDataString(_options.DefaultLanguage))
            .ToString();

        using var request = BuildRequest(HttpMethod.Get, path.ToString());
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<TmdbSearchResponse>(stream, SerializerOptions, cancellationToken);
        return payload ?? CreateEmptySearchResponse();
    }

    public async Task<TmdbImageConfiguration> GetImageConfigurationAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConfigurationAsync(cancellationToken);
        return _configuration?.Images ?? new TmdbImageConfiguration();
    }

    public async Task<TmdbMovieDetails?> GetMovieDetailsAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        if (tmdbId <= 0)
        {
            return null;
        }

        var path = new StringBuilder($"movie/{tmdbId}")
            .Append("?append_to_response=credits,images,external_ids,release_dates,watch/providers")
            .Append("&language=").Append(Uri.EscapeDataString(_options.DefaultLanguage))
            .Append("&region=").Append(Uri.EscapeDataString(_options.DefaultRegion))
            .ToString();

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

        var path = new StringBuilder($"tv/{tmdbId}")
            .Append("?append_to_response=aggregate_credits,credits,images,external_ids,content_ratings,watch/providers")
            .Append("&language=").Append(Uri.EscapeDataString(_options.DefaultLanguage))
            .Append("&region=").Append(Uri.EscapeDataString(_options.DefaultRegion))
            .ToString();

        using var request = BuildRequest(HttpMethod.Get, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<TmdbTvDetails>(stream, SerializerOptions, cancellationToken);
    }

    public async Task<TmdbWatchProviders?> GetWatchProvidersAsync(int tmdbId, string mediaType, CancellationToken cancellationToken = default)
    {
        if (tmdbId <= 0)
        {
            return null;
        }

        var normalized = NormalizeSearchType(mediaType);
        var path = normalized switch
        {
            "movie" => $"movie/{tmdbId}/watch/providers",
            "tv" => $"tv/{tmdbId}/watch/providers",
            _ => null
        };

        if (path is null)
        {
            return null;
        }

        using var request = BuildRequest(HttpMethod.Get, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<TmdbWatchProviders>(stream, SerializerOptions, cancellationToken);
    }

    public async Task<TmdbSeasonDetails?> GetTvSeasonAsync(int tmdbId, int seasonNumber, CancellationToken cancellationToken = default)
    {
        if (tmdbId <= 0 || seasonNumber < 0)
        {
            return null;
        }

        var path = new StringBuilder($"tv/{tmdbId}/season/{seasonNumber}")
            .Append("?language=").Append(Uri.EscapeDataString(_options.DefaultLanguage))
            .ToString();

        using var request = BuildRequest(HttpMethod.Get, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<TmdbSeasonDetails>(stream, SerializerOptions, cancellationToken);
    }

    private async Task EnsureConfigurationAsync(CancellationToken cancellationToken)
    {
        if (_configuration is not null)
        {
            return;
        }

        await _configurationLock.WaitAsync(cancellationToken);
        try
        {
            if (_configuration is not null)
            {
                return;
            }

            using var request = BuildRequest(HttpMethod.Get, "configuration");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            _configuration = await JsonSerializer.DeserializeAsync<TmdbConfiguration>(stream, SerializerOptions, cancellationToken);
        }
        finally
        {
            _configurationLock.Release();
        }
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("TMDB API key is not configured. Set the Tmdb:ApiKey configuration value.");
        }

        var uriBuilder = new StringBuilder(relativeUrl);
        uriBuilder.Append(relativeUrl.Contains('?') ? "&" : "?")
            .Append("api_key=").Append(Uri.EscapeDataString(_options.ApiKey));

        var request = new HttpRequestMessage(method, uriBuilder.ToString());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static string NormalizeSearchType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "multi";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "movie" or "tv" or "person" or "multi"
            ? normalized
            : "multi";
    }

    private static TmdbSearchResponse CreateEmptySearchResponse()
        => new()
        {
            Page = 1,
            TotalPages = 1,
            TotalResults = 0,
            Results = Array.Empty<TmdbSearchResult>()
        };
}

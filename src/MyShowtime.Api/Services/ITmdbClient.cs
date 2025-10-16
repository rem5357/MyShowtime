using MyShowtime.Api.Services.Models;

namespace MyShowtime.Api.Services;

public interface ITmdbClient
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<TmdbSearchResponse> SearchAsync(string query, string mediaType, int page, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TmdbPersonResult>> SearchPeopleAsync(string query, CancellationToken cancellationToken = default);
    Task<TmdbPersonMovieCredits?> GetPersonMovieCreditsAsync(int personId, CancellationToken cancellationToken = default);
    Task<TmdbSearchResponse> GetTrendingAsync(int page, CancellationToken cancellationToken = default);
    Task<TmdbImageConfiguration> GetImageConfigurationAsync(CancellationToken cancellationToken = default);
    Task<TmdbMovieDetails?> GetMovieDetailsAsync(int tmdbId, CancellationToken cancellationToken = default);
    Task<TmdbTvDetails?> GetTvDetailsAsync(int tmdbId, CancellationToken cancellationToken = default);
    Task<TmdbSeasonDetails?> GetTvSeasonAsync(int tmdbId, int seasonNumber, CancellationToken cancellationToken = default);
    Task<TmdbWatchProviders?> GetWatchProvidersAsync(int tmdbId, string mediaType, CancellationToken cancellationToken = default);
}

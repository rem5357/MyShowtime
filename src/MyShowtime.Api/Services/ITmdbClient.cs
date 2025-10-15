using MyShowtime.Api.Services.Models;
using MyShowtime.Shared.Dtos;
using MyShowtime.Shared.Enums;

namespace MyShowtime.Api.Services;

public interface ITmdbClient
{
    Task<IReadOnlyList<TmdbSearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<TmdbMovieDetails?> GetMovieDetailsAsync(int tmdbId, CancellationToken cancellationToken = default);
    Task<TmdbTvDetails?> GetTvDetailsAsync(int tmdbId, CancellationToken cancellationToken = default);
    Task<TmdbSeasonDetails?> GetTvSeasonAsync(int tmdbId, int seasonNumber, CancellationToken cancellationToken = default);
    Task<TmdbWatchProviders?> GetWatchProvidersAsync(int tmdbId, MediaType mediaType, CancellationToken cancellationToken = default);
}

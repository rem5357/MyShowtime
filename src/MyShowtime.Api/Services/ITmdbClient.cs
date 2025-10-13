using MyShowtime.Shared.Dtos;

namespace MyShowtime.Api.Services;

public interface ITmdbClient
{
    Task<IReadOnlyList<TmdbSearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<TmdbDetailsDto?> GetDetailsAsync(int tmdbId, string mediaType, CancellationToken cancellationToken = default);
}

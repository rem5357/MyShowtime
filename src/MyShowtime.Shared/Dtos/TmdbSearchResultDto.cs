using MyShowtime.Shared.Enums;

namespace MyShowtime.Shared.Dtos;

public record TmdbSearchResultDto(
    int TmdbId,
    MediaType MediaType,
    string Title,
    string? Overview,
    string? PosterPath,
    string? ReleaseDate,
    string? AvailableOn);

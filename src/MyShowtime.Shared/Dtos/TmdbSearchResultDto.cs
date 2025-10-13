namespace MyShowtime.Shared.Dtos;

public record TmdbSearchResultDto(
    int TmdbId,
    string MediaType,
    string Title,
    string? Overview,
    string? PosterPath,
    string? ReleaseDate);

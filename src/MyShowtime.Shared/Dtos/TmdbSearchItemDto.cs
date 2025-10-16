namespace MyShowtime.Shared.Dtos;

public record TmdbSearchItemDto(
    int Id,
    string MediaType,
    string Title,
    string SubTitle,
    string? Overview,
    string? PosterUrl,
    double Popularity,
    string? Source,
    string? ReleaseDate);

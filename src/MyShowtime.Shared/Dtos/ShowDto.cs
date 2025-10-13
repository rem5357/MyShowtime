namespace MyShowtime.Shared.Dtos;

public record ShowDto(
    Guid Id,
    int TmdbId,
    string Title,
    string MediaType,
    DateOnly? ReleaseDate,
    string? Overview,
    string? PosterPath,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

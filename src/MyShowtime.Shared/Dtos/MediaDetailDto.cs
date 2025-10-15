using MyShowtime.Shared.Enums;

namespace MyShowtime.Shared.Dtos;

public record MediaDetailDto(
    Guid Id,
    int TmdbId,
    MediaType MediaType,
    string Title,
    DateOnly? ReleaseDate,
    int Priority,
    string? Source,
    ViewState WatchState,
    bool Hidden,
    string? Synopsis,
    string? PosterPath,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Cast,
    string? Notes,
    string? AvailableOn,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? LastSyncedAtUtc);

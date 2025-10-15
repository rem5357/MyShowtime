using MyShowtime.Shared.Enums;

namespace MyShowtime.Shared.Dtos;

public record MediaSummaryDto(
    Guid Id,
    int TmdbId,
    MediaType MediaType,
    string Title,
    DateOnly? ReleaseDate,
    int Priority,
    string? Source,
    ViewState WatchState,
    bool Hidden,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? LastSyncedAtUtc);

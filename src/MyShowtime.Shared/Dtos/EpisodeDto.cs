using MyShowtime.Shared.Enums;

namespace MyShowtime.Shared.Dtos;

public record EpisodeDto(
    Guid Id,
    Guid MediaId,
    int TmdbEpisodeId,
    int SeasonNumber,
    int EpisodeNumber,
    string Title,
    DateOnly? AirDate,
    bool IsSpecial,
    ViewState WatchState,
    string? Synopsis);

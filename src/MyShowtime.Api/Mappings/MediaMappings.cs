using System.Text.Json;
using MyShowtime.Api.Entities;
using MyShowtime.Shared.Dtos;

namespace MyShowtime.Api.Mappings;

public static class MediaMappings
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static MediaSummaryDto ToSummaryDto(this Media entity) =>
        new(
            entity.Id,
            entity.TmdbId,
            entity.MediaType,
            entity.Title,
            entity.ReleaseDate,
            entity.Priority,
            entity.Source,
            entity.WatchState,
            entity.Hidden,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.LastSyncedAtUtc);

    public static MediaDetailDto ToDetailDto(this Media entity) =>
        new(
            entity.Id,
            entity.TmdbId,
            entity.MediaType,
            entity.Title,
            entity.ReleaseDate,
            entity.Priority,
            entity.Source,
            entity.WatchState,
            entity.Hidden,
            entity.Synopsis,
            entity.PosterPath,
            DeserializeList(entity.Genres),
            DeserializeList(entity.Cast),
            entity.Notes,
            entity.AvailableOn,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.LastSyncedAtUtc);

    public static EpisodeDto ToDto(this Episode entity) =>
        new(
            entity.Id,
            entity.MediaId,
            entity.TmdbEpisodeId,
            entity.SeasonNumber,
            entity.EpisodeNumber,
            entity.Title,
            entity.AirDate,
            entity.IsSpecial,
            entity.WatchState,
            entity.Synopsis);

    public static string SerializeList(IReadOnlyCollection<string>? values) =>
        values is null || values.Count == 0
            ? string.Empty
            : JsonSerializer.Serialize(values, JsonOptions);

    public static IReadOnlyList<string> DeserializeList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        try
        {
            var result = JsonSerializer.Deserialize<List<string>>(value, JsonOptions);
            return result ?? new List<string>();
        }
        catch
        {
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}

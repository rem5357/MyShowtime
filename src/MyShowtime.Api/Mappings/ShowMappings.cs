using MyShowtime.Api.Entities;
using MyShowtime.Shared.Dtos;

namespace MyShowtime.Api.Mappings;

public static class ShowMappings
{
    public static ShowDto ToDto(this Show entity) =>
        new(
            entity.Id,
            entity.TmdbId,
            entity.Title,
            entity.MediaType,
            entity.ReleaseDate,
            entity.Overview,
            entity.PosterPath,
            entity.Notes,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
}

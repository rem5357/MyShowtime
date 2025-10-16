namespace MyShowtime.Shared.Dtos;

public record TmdbSearchResponseDto(
    IReadOnlyList<TmdbSearchItemDto> Results,
    int Page,
    int TotalPages,
    int TotalResults,
    bool ServedFromCache);

public static class TmdbSearchResponseDtoExtensions
{
    public static readonly TmdbSearchResponseDto Empty = new(
        Array.Empty<TmdbSearchItemDto>(),
        1,
        1,
        0,
        false);
}

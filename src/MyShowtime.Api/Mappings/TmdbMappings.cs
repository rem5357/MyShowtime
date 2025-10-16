using System.Globalization;
using System.Linq;
using MyShowtime.Api.Services.Models;
using MyShowtime.Shared.Dtos;

namespace MyShowtime.Api.Mappings;

public static class TmdbMappings
{
    private const string DefaultPosterSize = "w342";

    public static TmdbSearchResponseDto ToSearchResponseDto(
        TmdbSearchResponse source,
        TmdbImageConfiguration images,
        bool servedFromCache,
        bool includePeople)
    {
        var baseUrl = !string.IsNullOrWhiteSpace(images.SecureBaseUrl)
            ? images.SecureBaseUrl
            : images.BaseUrl;

        var posterSize = SelectPosterSize(images);

        var sanitizedResults = source.Results
            .Where(result => result.Id > 0 && !string.IsNullOrWhiteSpace(result.MediaType))
            .ToList();

        var filteredResults = includePeople
            ? sanitizedResults
            : sanitizedResults
                .Where(result => !string.Equals(NormalizeMediaType(result.MediaType), "person", StringComparison.OrdinalIgnoreCase))
                .ToList();

        var results = filteredResults
            .Select(result => MapItem(result, baseUrl, posterSize))
            .Take(200)
            .ToList();

        var page = source.Page <= 0 ? 1 : source.Page;
        var totalPages = source.TotalPages <= 0 ? 1 : source.TotalPages;
        var removedCount = sanitizedResults.Count - filteredResults.Count;
        var adjustedTotal = includePeople ? source.TotalResults : Math.Max(0, source.TotalResults - removedCount);
        if (adjustedTotal == 0 && filteredResults.Count > 0)
        {
            adjustedTotal = filteredResults.Count;
        }

        return new TmdbSearchResponseDto(results, page, totalPages, adjustedTotal, servedFromCache);
    }

    private static TmdbSearchItemDto MapItem(TmdbSearchResult source, string baseUrl, string posterSize)
    {
        var mediaType = NormalizeMediaType(source.MediaType);
        var (title, releaseDate) = ResolveTitleAndRelease(source, mediaType);
        var subtitle = BuildSubtitle(mediaType, releaseDate);
        var posterPath = mediaType == "person" ? source.ProfilePath : source.PosterPath;

        return new TmdbSearchItemDto(
            source.Id,
            mediaType,
            title,
            subtitle,
            source.Overview,
            BuildPosterUrl(baseUrl, posterSize, posterPath),
            source.Popularity,
            null,
            releaseDate);
    }

    private static (string Title, string? ReleaseDate) ResolveTitleAndRelease(TmdbSearchResult source, string mediaType)
        => mediaType switch
        {
            "movie" => (FirstNonEmpty(source.Title, source.Name, "Untitled"), NormalizeDate(source.ReleaseDate)),
            "tv" => (FirstNonEmpty(source.Name, source.Title, "Untitled"), NormalizeDate(source.FirstAirDate ?? source.ReleaseDate)),
            "person" => (FirstNonEmpty(source.Name, source.Title, "Unknown"), null),
            _ => (FirstNonEmpty(source.Title, source.Name, "Untitled"), NormalizeDate(source.ReleaseDate))
        };

    private static string BuildSubtitle(string mediaType, string? releaseDate)
    {
        var year = ExtractYear(releaseDate);

        return mediaType switch
        {
            "movie" => year is null ? "Movie" : $"Movie • {year}",
            "tv" => year is null ? "TV" : $"TV • {year}",
            "person" => "Person",
            _ => year is null ? mediaType.ToUpperInvariant() : $"{mediaType.ToUpperInvariant()} • {year}"
        };
    }

    private static string? BuildPosterUrl(string baseUrl, string size, string? path)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        return $"{baseUrl}{size}{path}";
    }

    private static string SelectPosterSize(TmdbImageConfiguration images)
    {
        if (images.PosterSizes is { Count: > 0 })
        {
            if (images.PosterSizes.Contains(DefaultPosterSize))
            {
                return DefaultPosterSize;
            }

            return images.PosterSizes[^1];
        }

        return DefaultPosterSize;
    }

    private static string NormalizeMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return "movie";
        }

        var normalized = mediaType.Trim().ToLowerInvariant();
        return normalized is "movie" or "tv" or "person" ? normalized : normalized;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value!;
            }
        }

        return string.Empty;
    }

    private static string? NormalizeDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    private static string? ExtractYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, out var parsed)
            ? parsed.Year.ToString(CultureInfo.InvariantCulture)
            : null;
    }

    public static string? CreatePosterUrl(TmdbImageConfiguration images, string? path)
    {
        var baseUrl = !string.IsNullOrWhiteSpace(images.SecureBaseUrl)
            ? images.SecureBaseUrl
            : images.BaseUrl;

        var size = SelectPosterSize(images);
        return BuildPosterUrl(baseUrl, size, path);
    }
}

using System.Text.Json.Serialization;

namespace MyShowtime.Api.Services.Models;

public record TmdbSearchResponse
{
    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; init; }

    [JsonPropertyName("total_results")]
    public int TotalResults { get; init; }

    [JsonPropertyName("results")]
    public IReadOnlyList<TmdbSearchResult> Results { get; init; } = Array.Empty<TmdbSearchResult>();
}

public record TmdbSearchResult
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("media_type")]
    public string MediaType { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; init; }

    [JsonPropertyName("profile_path")]
    public string? ProfilePath { get; init; }

    [JsonPropertyName("popularity")]
    public double Popularity { get; init; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; init; }

    [JsonPropertyName("first_air_date")]
    public string? FirstAirDate { get; init; }
}

public record TmdbConfiguration
{
    [JsonPropertyName("images")]
    public TmdbImageConfiguration Images { get; init; } = new();
}

public record TmdbImageConfiguration
{
    [JsonPropertyName("base_url")]
    public string BaseUrl { get; init; } = string.Empty;

    [JsonPropertyName("secure_base_url")]
    public string SecureBaseUrl { get; init; } = string.Empty;

    [JsonPropertyName("poster_sizes")]
    public IReadOnlyList<string> PosterSizes { get; init; } = Array.Empty<string>();
}

public record TmdbPersonSearchResponse
{
    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; init; }

    [JsonPropertyName("total_results")]
    public int TotalResults { get; init; }

    [JsonPropertyName("results")]
    public IReadOnlyList<TmdbPersonResult> Results { get; init; } = Array.Empty<TmdbPersonResult>();
}

public record TmdbPersonResult
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public record TmdbPersonMovieCredits
{
    [JsonPropertyName("cast")]
    public IReadOnlyList<TmdbMovieCreditRole> Cast { get; init; } = Array.Empty<TmdbMovieCreditRole>();

    [JsonPropertyName("crew")]
    public IReadOnlyList<TmdbMovieCreditRole> Crew { get; init; } = Array.Empty<TmdbMovieCreditRole>();
}

public record TmdbMovieCreditRole
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("original_title")]
    public string? OriginalTitle { get; init; }

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; init; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; init; }

    [JsonPropertyName("popularity")]
    public double Popularity { get; init; }

    [JsonPropertyName("character")]
    public string? Character { get; init; }

    [JsonPropertyName("job")]
    public string? Job { get; init; }

    [JsonPropertyName("department")]
    public string? Department { get; init; }
}

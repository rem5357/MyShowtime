using System.Text.Json.Serialization;

namespace MyShowtime.Api.Services.Models;

public record TmdbGenre
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public record TmdbCastMember
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("character")]
    public string? Character { get; init; }

    [JsonPropertyName("order")]
    public int Order { get; init; }
}

public record TmdbCredits
{
    [JsonPropertyName("cast")]
    public IReadOnlyList<TmdbCastMember> Cast { get; init; } = Array.Empty<TmdbCastMember>();
}

public record TmdbWatchProviderCountry
{
    [JsonPropertyName("link")]
    public string? Link { get; init; }

    [JsonPropertyName("flatrate")]
    public IReadOnlyList<TmdbWatchProviderEntry>? Flatrate { get; init; }

    [JsonPropertyName("ads")]
    public IReadOnlyList<TmdbWatchProviderEntry>? Ads { get; init; }

    [JsonPropertyName("rent")]
    public IReadOnlyList<TmdbWatchProviderEntry>? Rent { get; init; }

    [JsonPropertyName("buy")]
    public IReadOnlyList<TmdbWatchProviderEntry>? Buy { get; init; }
}

public record TmdbWatchProviderEntry
{
    [JsonPropertyName("provider_id")]
    public int ProviderId { get; init; }

    [JsonPropertyName("provider_name")]
    public string ProviderName { get; init; } = string.Empty;
}

public record TmdbWatchProviders
{
    [JsonPropertyName("results")]
    public Dictionary<string, TmdbWatchProviderCountry> Results { get; init; } = new();
}

public record TmdbSeasonInfo
{
    [JsonPropertyName("season_number")]
    public int SeasonNumber { get; init; }

    [JsonPropertyName("episode_count")]
    public int EpisodeCount { get; init; }

    [JsonPropertyName("air_date")]
    public string? AirDate { get; init; }
}

public record TmdbEpisode
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("air_date")]
    public string? AirDate { get; init; }

    [JsonPropertyName("season_number")]
    public int SeasonNumber { get; init; }

    [JsonPropertyName("episode_number")]
    public int EpisodeNumber { get; init; }

    [JsonPropertyName("still_path")]
    public string? StillPath { get; init; }
}

public record TmdbSeasonDetails
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("season_number")]
    public int SeasonNumber { get; init; }

    [JsonPropertyName("episodes")]
    public IReadOnlyList<TmdbEpisode> Episodes { get; init; } = Array.Empty<TmdbEpisode>();
}

public record TmdbMovieDetails
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; init; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; init; }

    [JsonPropertyName("genres")]
    public IReadOnlyList<TmdbGenre> Genres { get; init; } = Array.Empty<TmdbGenre>();

    [JsonPropertyName("credits")]
    public TmdbCredits Credits { get; init; } = new();

    [JsonPropertyName("watch/providers")]
    public TmdbWatchProviders WatchProviders { get; init; } = new();
}

public record TmdbTvDetails
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("first_air_date")]
    public string? FirstAirDate { get; init; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; init; }

    [JsonPropertyName("genres")]
    public IReadOnlyList<TmdbGenre> Genres { get; init; } = Array.Empty<TmdbGenre>();

    [JsonPropertyName("credits")]
    public TmdbCredits Credits { get; init; } = new();

    [JsonPropertyName("aggregate_credits")]
    public TmdbCredits? AggregateCredits { get; init; }

    [JsonPropertyName("watch/providers")]
    public TmdbWatchProviders WatchProviders { get; init; } = new();

    [JsonPropertyName("seasons")]
    public IReadOnlyList<TmdbSeasonInfo> Seasons { get; init; } = Array.Empty<TmdbSeasonInfo>();

    [JsonPropertyName("networks")]
    public IReadOnlyList<TmdbNetwork> Networks { get; init; } = Array.Empty<TmdbNetwork>();
}

public record TmdbNetwork
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("origin_country")]
    public string? OriginCountry { get; init; }
}

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyShowtime.Api.Data;
using MyShowtime.Api.Entities;
using MyShowtime.Api.Mappings;
using MyShowtime.Api.Options;
using MyShowtime.Api.Services;
using MyShowtime.Api.Services.Models;
using MyShowtime.Shared.Dtos;
using MyShowtime.Shared.Enums;
using MyShowtime.Shared.Requests;
using Polly;
using Polly.Retry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
builder.Services.AddMemoryCache(options => options.SizeLimit = 200);

builder.Services.Configure<TmdbOptions>(builder.Configuration.GetSection(TmdbOptions.SectionName));

builder.Services.AddHttpClient<ITmdbClient, TmdbClient>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TmdbOptions>>().Value;
    httpClient.BaseAddress = new Uri(options.BaseAddress);
    httpClient.Timeout = TimeSpan.FromSeconds(20);
})
.SetHandlerLifetime(TimeSpan.FromMinutes(5))
.AddPolicyHandler(CreateTmdbRetryPolicy());

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCompression();
app.UseStatusCodePages();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    var tmdbClient = scope.ServiceProvider.GetRequiredService<ITmdbClient>();
    await tmdbClient.InitializeAsync();
}

app.MapGet("/api/status", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/tmdb/search", async (
    [FromQuery(Name = "q")] string? query,
    [FromQuery] string? type,
    [FromQuery] int? page,
    ITmdbClient tmdbClient,
    IMemoryCache cache,
    ILoggerFactory loggerFactory,
    IOptions<TmdbOptions> tmdbOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var logger = loggerFactory.CreateLogger("TmdbSearch");
        var trimmedQuery = (query ?? string.Empty).Trim();
        var normalizedType = NormalizeSearchType(type);
        var isPersonSearch = normalizedType == "person";

        if (isPersonSearch && trimmedQuery.Length > 0 && trimmedQuery.Length < 4)
        {
            return Results.BadRequest(new { message = "People search requires at least four characters." });
        }

        if (!isPersonSearch && !string.IsNullOrWhiteSpace(trimmedQuery) && trimmedQuery.Length < 2)
        {
            return Results.Ok(TmdbSearchResponseDtoExtensions.Empty);
        }

        var sanitizedPage = !isPersonSearch && page is > 0 ? page.Value : 1;
        var options = tmdbOptions.Value;
        var languageKey = options.DefaultLanguage ?? string.Empty;
        var regionKey = options.DefaultRegion ?? string.Empty;
        var isTrending = !isPersonSearch && string.IsNullOrWhiteSpace(trimmedQuery);

        var cacheKey = isPersonSearch
            ? $"tmdb:people:{trimmedQuery.ToLowerInvariant()}:{languageKey}"
            : (isTrending
                ? $"tmdb:trending:{sanitizedPage}:{languageKey}:{regionKey}"
                : $"tmdb:search:{normalizedType}:{trimmedQuery.ToLowerInvariant()}:{sanitizedPage}:{languageKey}:{regionKey}");

        TmdbSearchResponseDto dto;
        if (cache.TryGetValue(cacheKey, out TmdbSearchResponseDto? cached) && cached is not null)
        {
            dto = cached with { ServedFromCache = true };
            logger.LogDebug(
                "TMDB {Kind} cache hit (query=\"{Query}\", page={Page})",
                isPersonSearch ? "search:person" : (isTrending ? "trending" : $"search:{normalizedType}"),
                trimmedQuery,
                sanitizedPage);
        }
        else
        {
            var stopwatch = Stopwatch.StartNew();

            if (isPersonSearch)
            {
                var images = await tmdbClient.GetImageConfigurationAsync(cancellationToken);
                dto = await BuildPeopleSearchResponseAsync(trimmedQuery, tmdbClient, images, cancellationToken);
            }
            else
            {
                var responsePayload = isTrending
                    ? await tmdbClient.GetTrendingAsync(sanitizedPage, cancellationToken)
                    : await tmdbClient.SearchAsync(trimmedQuery, normalizedType, sanitizedPage, cancellationToken);

                var images = await tmdbClient.GetImageConfigurationAsync(cancellationToken);
                dto = TmdbMappings.ToSearchResponseDto(
                    responsePayload,
                    images,
                    false,
                    includePeople: false);
            }

            dto = await EnrichSearchSourcesAsync(dto, tmdbClient, cache, cancellationToken);

            stopwatch.Stop();

            cache.Set(cacheKey, dto with { ServedFromCache = false }, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                Size = 1
            });

            logger.LogInformation(
                "TMDB {Kind} cache miss (query=\"{Query}\", page={Page}, results={Results}, durationMs={Duration})",
                isPersonSearch ? "search:person" : (isTrending ? "trending" : $"search:{normalizedType}"),
                trimmedQuery,
                sanitizedPage,
                dto.Results.Count,
                stopwatch.Elapsed.TotalMilliseconds);
        }

        var etag = ComputeSearchEtag(dto);
        httpContext.Response.Headers.CacheControl = "public,max-age=300";
        httpContext.Response.Headers.ETag = etag;
        httpContext.Response.Headers["X-Cache"] = dto.ServedFromCache ? "HIT" : "MISS";

        if (httpContext.Request.Headers.TryGetValue("If-None-Match", out var providedEtags)
            && providedEtags.Contains(etag))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Ok(dto);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem($"TMDB request failed: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/tmdb/details", async ([FromQuery] int tmdbId, [FromQuery] string mediaType, ITmdbClient tmdbClient, CancellationToken cancellationToken) =>
{
    if (tmdbId <= 0)
    {
        return Results.BadRequest(new { message = "A valid TMDB id is required." });
    }

    var normalizedType = NormalizeSearchType(mediaType);
    if (normalizedType == "person")
    {
        return Results.BadRequest(new { message = "Person details are not supported." });
    }

    try
    {
        var now = DateTime.UtcNow;
        MediaDetailDto? payload = normalizedType switch
        {
            "movie" => await BuildMoviePreviewAsync(tmdbClient, tmdbId, now, cancellationToken),
            "tv" => await BuildTvPreviewAsync(tmdbClient, tmdbId, now, cancellationToken),
            _ => null
        };

        return payload is null ? Results.Problem("Unable to retrieve details from TMDB.", statusCode: StatusCodes.Status502BadGateway) : Results.Ok(payload);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem($"TMDB request failed: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
    }
});

static async Task<TmdbSearchResponseDto> BuildPeopleSearchResponseAsync(string query, ITmdbClient tmdbClient, TmdbImageConfiguration images, CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(query))
    {
        return TmdbSearchResponseDtoExtensions.Empty;
    }

    var people = await tmdbClient.SearchPeopleAsync(query, cancellationToken);
    if (people.Count == 0)
    {
        return TmdbSearchResponseDtoExtensions.Empty;
    }

    var creditTasks = people
        .Select(person => tmdbClient.GetPersonMovieCreditsAsync(person.Id, cancellationToken))
        .ToArray();

    await Task.WhenAll(creditTasks);

    var movies = new Dictionary<int, PersonMovieAggregation>();

    for (var i = 0; i < people.Count; i++)
    {
        var credits = creditTasks[i].Result;
        if (credits is null)
        {
            continue;
        }

        var personName = people[i].Name;
        ProcessCredits(credits.Cast, personName, true);
        ProcessCredits(credits.Crew, personName, false);
    }

    if (movies.Count == 0)
    {
        return TmdbSearchResponseDtoExtensions.Empty;
    }

    var items = movies.Values
        .OrderByDescending(m => m.Popularity)
        .ThenBy(m => m.Title, StringComparer.OrdinalIgnoreCase)
        .Take(200)
        .Select(MapToDto)
        .ToList();

    return new TmdbSearchResponseDto(items, 1, 1, items.Count, false);

    void ProcessCredits(IReadOnlyList<TmdbMovieCreditRole> entries, string personName, bool isCast)
    {
        if (entries is null)
        {
            return;
        }

        foreach (var entry in entries)
        {
            if (entry is null || entry.Id <= 0)
            {
                continue;
            }

            var title = string.IsNullOrWhiteSpace(entry.Title) ? entry.OriginalTitle : entry.Title;
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            if (!movies.TryGetValue(entry.Id, out var aggregation))
            {
                aggregation = new PersonMovieAggregation(entry.Id, title!);
                movies[entry.Id] = aggregation;
            }

            var normalizedDate = NormalizeDateValue(entry.ReleaseDate);
            aggregation.Update(normalizedDate, entry.Overview, entry.PosterPath, entry.Popularity);

            var role = isCast
                ? (string.IsNullOrWhiteSpace(entry.Character) ? "Cast" : entry.Character)
                : (!string.IsNullOrWhiteSpace(entry.Job)
                    ? entry.Job
                    : (string.IsNullOrWhiteSpace(entry.Department) ? "Crew" : entry.Department));

            aggregation.AddContribution(personName, role);
        }
    }

    TmdbSearchItemDto MapToDto(PersonMovieAggregation aggregation)
    {
        var poster = TmdbMappings.CreatePosterUrl(images, aggregation.PosterPath);
        var year = ExtractYear(aggregation.ReleaseDate);
        var subtitle = year is null ? "Movie" : $"Movie • {year}";

        var contributors = aggregation.Contributors.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
        if (contributors.Count > 0)
        {
            var contributionText = contributors.Count > 3
                ? string.Join(", ", contributors.Take(3)) + $" +{contributors.Count - 3} more"
                : string.Join(", ", contributors);
            subtitle = $"{subtitle} • {contributionText}";
        }

        return new TmdbSearchItemDto(
            aggregation.Id,
            "movie",
            aggregation.Title,
            subtitle,
            aggregation.Overview,
            poster,
            aggregation.Popularity,
            null,
            aggregation.ReleaseDate);
    }

    static string? NormalizeDateValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    static string? ExtractYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, out var parsed)
            ? parsed.Year.ToString(CultureInfo.InvariantCulture)
            : null;
    }
}

app.MapGet("/api/media", async ([FromQuery] bool? includeHidden, ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    var includeHiddenValue = includeHidden ?? false;

    var query = db.Media.AsNoTracking();
    if (!includeHiddenValue)
    {
        query = query.Where(m => !m.Hidden);
    }

    var items = await query
        .OrderBy(m => m.Priority)
        .ThenBy(m => m.Title)
        .ToListAsync(cancellationToken);

    return Results.Ok(items.Select(m => m.ToSummaryDto()));
});

app.MapGet("/api/media/{id:guid}", async (Guid id, ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    var media = await db.Media.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    return media is null ? Results.NotFound() : Results.Ok(media.ToDetailDto());
});

app.MapGet("/api/media/{id:guid}/episodes", async (Guid id, ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    var episodes = await db.Episodes
        .AsNoTracking()
        .Where(e => e.MediaId == id)
        .OrderBy(e => e.SeasonNumber)
        .ThenBy(e => e.EpisodeNumber)
        .ToListAsync(cancellationToken);

    return Results.Ok(episodes.Select(e => e.ToDto()));
});

app.MapPost("/api/media/import", async ([FromBody] ImportMediaRequest request, ApplicationDbContext db, ITmdbClient tmdbClient, CancellationToken cancellationToken) =>
{
    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(request);
    if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
    {
        var errors = validationResults
            .GroupBy(r => r.MemberNames.FirstOrDefault() ?? string.Empty, r => r.ErrorMessage ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.ToArray());
        return Results.ValidationProblem(errors);
    }

    var existing = await db.Media.FirstOrDefaultAsync(m => m.TmdbId == request.TmdbId, cancellationToken);
    var now = DateTime.UtcNow;

    try
    {
        Media entity;
        if (existing is null)
        {
            entity = new Media
            {
                TmdbId = request.TmdbId,
                MediaType = request.MediaType,
                Priority = request.Priority ?? 3,
                CreatedAtUtc = now
            };
            db.Media.Add(entity);
        }
        else
        {
            entity = existing;
            entity.Priority = request.Priority ?? entity.Priority;
            entity.UpdatedAtUtc = now;
        }

        switch (request.MediaType)
        {
            case MediaType.Movie:
                var movie = await tmdbClient.GetMovieDetailsAsync(request.TmdbId, cancellationToken);
                if (movie is null)
                {
                    return Results.Problem("Unable to retrieve movie details from TMDB.", statusCode: StatusCodes.Status502BadGateway);
                }
                await PopulateMovieAsync(entity, movie, db, cancellationToken, now);
                break;

            case MediaType.TvShow:
                var tv = await tmdbClient.GetTvDetailsAsync(request.TmdbId, cancellationToken);
                if (tv is null)
                {
                    return Results.Problem("Unable to retrieve TV show details from TMDB.", statusCode: StatusCodes.Status502BadGateway);
                }
                await PopulateTvShowAsync(entity, tv, tmdbClient, db, cancellationToken, now);
                break;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(entity.ToDetailDto());
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem($"TMDB request failed: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPost("/api/media/{id:guid}/sync", async (Guid id, ApplicationDbContext db, ITmdbClient tmdbClient, CancellationToken cancellationToken) =>
{
    var entity = await db.Media.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    if (entity is null)
    {
        return Results.NotFound();
    }

    var now = DateTime.UtcNow;

    try
    {
        switch (entity.MediaType)
        {
            case MediaType.Movie:
                var movie = await tmdbClient.GetMovieDetailsAsync(entity.TmdbId, cancellationToken);
                if (movie is null)
                {
                    return Results.Problem("Unable to retrieve movie details from TMDB.", statusCode: StatusCodes.Status502BadGateway);
                }
                await PopulateMovieAsync(entity, movie, db, cancellationToken, now);
                break;

            case MediaType.TvShow:
                var tv = await tmdbClient.GetTvDetailsAsync(entity.TmdbId, cancellationToken);
                if (tv is null)
                {
                    return Results.Problem("Unable to retrieve TV show details from TMDB.", statusCode: StatusCodes.Status502BadGateway);
                }
                await PopulateTvShowAsync(entity, tv, tmdbClient, db, cancellationToken, now);
                break;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(entity.ToDetailDto());
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem($"TMDB request failed: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPut("/api/media/{id:guid}", async (Guid id, [FromBody] UpdateMediaRequest request, ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(request);
    if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
    {
        var errors = validationResults
            .GroupBy(r => r.MemberNames.FirstOrDefault() ?? string.Empty, r => r.ErrorMessage ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.ToArray());
        return Results.ValidationProblem(errors);
    }

    var entity = await db.Media.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    if (entity is null)
    {
        return Results.NotFound();
    }

    entity.Priority = request.Priority;
    entity.WatchState = request.WatchState;
    entity.Hidden = request.Hidden;
    entity.Source = request.Source;
    entity.AvailableOn = request.AvailableOn;
    entity.Notes = request.Notes;
    entity.UpdatedAtUtc = DateTime.UtcNow;

    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(entity.ToDetailDto());
});

app.MapPut("/api/media/{mediaId:guid}/episodes/{episodeId:guid}/viewstate", async (Guid mediaId, Guid episodeId, [FromBody] UpdateEpisodeViewStateRequest request, ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    var episode = await db.Episodes.FirstOrDefaultAsync(e => e.Id == episodeId && e.MediaId == mediaId, cancellationToken);
    if (episode is null)
    {
        return Results.NotFound();
    }

    episode.WatchState = request.WatchState;
    episode.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(episode.ToDto());
});

app.Run();

static async Task<MediaDetailDto?> BuildMoviePreviewAsync(ITmdbClient tmdbClient, int tmdbId, DateTime timestamp, CancellationToken cancellationToken)
{
    var movie = await tmdbClient.GetMovieDetailsAsync(tmdbId, cancellationToken);
    return movie is null ? null : CreatePreviewFromMovie(movie, timestamp);
}

static async Task<MediaDetailDto?> BuildTvPreviewAsync(ITmdbClient tmdbClient, int tmdbId, DateTime timestamp, CancellationToken cancellationToken)
{
    var tv = await tmdbClient.GetTvDetailsAsync(tmdbId, cancellationToken);
    return tv is null ? null : CreatePreviewFromTvShow(tv, timestamp);
}

static async Task<TmdbSearchResponseDto> EnrichSearchSourcesAsync(
    TmdbSearchResponseDto dto,
    ITmdbClient tmdbClient,
    IMemoryCache cache,
    CancellationToken cancellationToken)
{
    if (dto.Results.Count == 0)
    {
        return dto;
    }

    var results = new List<TmdbSearchItemDto>(dto.Results.Count);
    using var semaphore = new SemaphoreSlim(3);

    var enrichmentTasks = dto.Results.Select(async item =>
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return item;
        }

        var mediaType = item.MediaType?.ToLowerInvariant();
        if (mediaType is not "movie" and not "tv")
        {
            return item with { Source = null };
        }

        var cacheKey = $"tmdb:source:{mediaType}:{item.Id}";
        if (cache.TryGetValue(cacheKey, out string? cachedSource))
        {
            return item with { Source = NormalizeSource(cachedSource) };
        }

        await semaphore.WaitAsync(cancellationToken);
        string? resolvedSource = null;
        try
        {
            resolvedSource = await FetchPrimarySourceAsync(tmdbClient, item.Id, mediaType, cancellationToken);
        }
        catch
        {
            // Ignore transient errors; leave source null for this item.
        }
        finally
        {
            semaphore.Release();
        }

        cache.Set(cacheKey, resolvedSource ?? string.Empty, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6),
            Size = 1
        });

        return item with { Source = NormalizeSource(resolvedSource) };
    });

    results = (await Task.WhenAll(enrichmentTasks)).ToList();

    return dto with { Results = results };
}

static async Task<string?> FetchPrimarySourceAsync(ITmdbClient tmdbClient, int tmdbId, string mediaType, CancellationToken cancellationToken)
{
    try
    {
        if (mediaType == "movie")
        {
            var details = await tmdbClient.GetMovieDetailsAsync(tmdbId, cancellationToken);
            return details is null
                ? null
                : SelectPrimaryProvider(details.WatchProviders);
        }

        var tvDetails = await tmdbClient.GetTvDetailsAsync(tmdbId, cancellationToken);
        if (tvDetails is null)
        {
            return null;
        }

        return SelectPrimaryProvider(tvDetails.WatchProviders)
            ?? tvDetails.Networks.FirstOrDefault()?.Name;
    }
    catch
    {
        return null;
    }
}

static string? NormalizeSource(string? value)
    => string.IsNullOrWhiteSpace(value) ? null : value;

static string ComputeSearchEtag(TmdbSearchResponseDto response)
{
    var sanitized = response with { ServedFromCache = false };
    var builder = new StringBuilder()
        .Append(sanitized.Page).Append('|')
        .Append(sanitized.TotalPages).Append('|')
        .Append(sanitized.TotalResults);

    foreach (var item in sanitized.Results)
    {
        builder.Append(';')
            .Append(item.Id).Append('|')
            .Append(item.MediaType).Append('|')
            .Append(item.Title).Append('|')
            .Append(item.SubTitle).Append('|')
            .Append(item.Source ?? string.Empty).Append('|')
            .Append(item.ReleaseDate ?? string.Empty).Append('|')
            .Append(item.Popularity.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    using var sha = SHA256.Create();
    var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
    return $"\"{Convert.ToHexString(hash)}\"";
}

static string NormalizeSearchType(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "multi";
    }

    var normalized = value.Trim().ToLowerInvariant();
    return normalized switch
    {
        "movie" => "movie",
        "tv" or "tvshow" or "television" => "tv",
        "media" => "multi",
        "person" => "person",
        _ => "multi"
    };
}

static IAsyncPolicy<HttpResponseMessage> CreateTmdbRetryPolicy()
    => Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
        .WaitAndRetryAsync(3, attempt =>
        {
            var jitter = Random.Shared.NextDouble() * 150;
            var backoff = Math.Pow(2, attempt - 1) * 250;
            return TimeSpan.FromMilliseconds(backoff + jitter);
        });

static async Task PopulateMovieAsync(Media entity, TmdbMovieDetails details, ApplicationDbContext db, CancellationToken cancellationToken, DateTime timestamp)
{
    entity.Title = string.IsNullOrWhiteSpace(details.Title) ? entity.Title : details.Title;
    entity.Synopsis = details.Overview ?? entity.Synopsis;
    entity.ReleaseDate = ParseDate(details.ReleaseDate);
    entity.PosterPath = details.PosterPath ?? entity.PosterPath;
    entity.Genres = MediaMappings.SerializeList(details.Genres.Select(g => g.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(5).ToList());
    entity.Cast = MediaMappings.SerializeList(details.Credits.Cast.OrderBy(c => c.Order).Select(c => c.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(6).ToList());
    entity.AvailableOn = SelectPrimaryProvider(details.WatchProviders);
    entity.Source = entity.AvailableOn;
    entity.MediaType = MediaType.Movie;
    entity.LastSyncedAtUtc = timestamp;
    entity.UpdatedAtUtc = timestamp;

    if (db.Entry(entity).IsKeySet)
    {
        await db.Episodes.Where(e => e.MediaId == entity.Id).ExecuteDeleteAsync(cancellationToken);
    }
    entity.Episodes.Clear();
}

static async Task PopulateTvShowAsync(Media entity, TmdbTvDetails details, ITmdbClient tmdbClient, ApplicationDbContext db, CancellationToken cancellationToken, DateTime timestamp)
{
    entity.Title = string.IsNullOrWhiteSpace(details.Name) ? entity.Title : details.Name;
    entity.Synopsis = details.Overview ?? entity.Synopsis;
    entity.ReleaseDate = ParseDate(details.FirstAirDate);
    entity.PosterPath = details.PosterPath ?? entity.PosterPath;
    entity.Genres = MediaMappings.SerializeList(details.Genres.Select(g => g.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(5).ToList());

    var castSource = details.AggregateCredits?.Cast?.Count > 0 ? details.AggregateCredits : details.Credits;
    entity.Cast = MediaMappings.SerializeList(castSource.Cast.OrderBy(c => c.Order).Select(c => c.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(8).ToList());
    entity.AvailableOn = SelectPrimaryProvider(details.WatchProviders);
    entity.Source = entity.AvailableOn;
    entity.MediaType = MediaType.TvShow;
    entity.LastSyncedAtUtc = timestamp;
    entity.UpdatedAtUtc = timestamp;

    if (db.Entry(entity).IsKeySet)
    {
        await db.Episodes.Where(e => e.MediaId == entity.Id).ExecuteDeleteAsync(cancellationToken);
    }

    var newEpisodes = new List<Episode>();

    foreach (var season in details.Seasons.OrderBy(s => s.SeasonNumber))
    {
        if (season.SeasonNumber < 0)
        {
            continue;
        }

        var seasonDetails = await tmdbClient.GetTvSeasonAsync(entity.TmdbId, season.SeasonNumber, cancellationToken);
        if (seasonDetails?.Episodes is null)
        {
            continue;
        }

        foreach (var episode in seasonDetails.Episodes)
        {
            var model = new Episode
            {
                MediaId = entity.Id,
                TmdbEpisodeId = episode.Id,
                SeasonNumber = episode.SeasonNumber,
                EpisodeNumber = episode.EpisodeNumber,
                Title = string.IsNullOrWhiteSpace(episode.Name) ? $"Episode {episode.EpisodeNumber}" : episode.Name,
                AirDate = ParseDate(episode.AirDate),
                Synopsis = episode.Overview,
                IsSpecial = episode.SeasonNumber == 0,
                WatchState = ViewState.Unwatched,
                CreatedAtUtc = timestamp
            };
            newEpisodes.Add(model);
        }
    }

    entity.Episodes.Clear();

    if (newEpisodes.Count > 0)
    {
        await db.Episodes.AddRangeAsync(newEpisodes, cancellationToken);
        foreach (var episode in newEpisodes)
        {
            entity.Episodes.Add(episode);
        }
    }
}

static MediaDetailDto CreatePreviewFromMovie(TmdbMovieDetails details, DateTime timestamp)
{
    var title = string.IsNullOrWhiteSpace(details.Title) ? "Untitled" : details.Title;
    var genres = details.Genres.Select(g => g.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(5).ToList();
    var cast = details.Credits.Cast.OrderBy(c => c.Order).Select(c => c.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(6).ToList();
    var availableOn = SelectPrimaryProvider(details.WatchProviders);

    return new MediaDetailDto(
        Guid.Empty,
        details.Id,
        MediaType.Movie,
        title,
        ParseDate(details.ReleaseDate),
        3,
        availableOn,
        ViewState.Unwatched,
        false,
        details.Overview,
        details.PosterPath,
        genres,
        cast,
        null,
        availableOn,
        timestamp,
        null,
        null);
}

static MediaDetailDto CreatePreviewFromTvShow(TmdbTvDetails details, DateTime timestamp)
{
    var title = string.IsNullOrWhiteSpace(details.Name) ? "Untitled" : details.Name;
    var genres = details.Genres.Select(g => g.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(5).ToList();
    var castSource = details.AggregateCredits?.Cast?.Count > 0 ? details.AggregateCredits : details.Credits;
    var cast = castSource.Cast.OrderBy(c => c.Order).Select(c => c.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(8).ToList();
    var availableOn = SelectPrimaryProvider(details.WatchProviders);

    return new MediaDetailDto(
        Guid.Empty,
        details.Id,
        MediaType.TvShow,
        title,
        ParseDate(details.FirstAirDate),
        3,
        availableOn,
        ViewState.Unwatched,
        false,
        details.Overview,
        details.PosterPath,
        genres,
        cast,
        null,
        availableOn,
        timestamp,
        null,
        null);
}

static DateOnly? ParseDate(string? value)
    => string.IsNullOrWhiteSpace(value) ? null : (DateOnly.TryParse(value, out var date) ? date : null);

static string? SelectPrimaryProvider(TmdbWatchProviders providers)
{
    if (providers?.Results is null || providers.Results.Count == 0)
    {
        return null;
    }

    var priorityCountries = new[] { "US", "CA", "GB", "AU" };
    foreach (var country in priorityCountries)
    {
        if (!providers.Results.TryGetValue(country, out var entry))
        {
            continue;
        }

        var provider = entry.Flatrate?.FirstOrDefault()
                      ?? entry.Ads?.FirstOrDefault()
                      ?? entry.Rent?.FirstOrDefault()
                      ?? entry.Buy?.FirstOrDefault();
        if (provider is not null)
        {
            return provider.ProviderName;
        }
    }

    // fallback: any provider
    var first = providers.Results.Values
        .SelectMany(v => (v.Flatrate ?? Array.Empty<TmdbWatchProviderEntry>())
            .Concat(v.Ads ?? Array.Empty<TmdbWatchProviderEntry>())
            .Concat(v.Rent ?? Array.Empty<TmdbWatchProviderEntry>())
            .Concat(v.Buy ?? Array.Empty<TmdbWatchProviderEntry>()))
        .FirstOrDefault();

    return first?.ProviderName;
}

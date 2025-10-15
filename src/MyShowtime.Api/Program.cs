using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.Configure<TmdbOptions>(builder.Configuration.GetSection(TmdbOptions.SectionName));

builder.Services.AddHttpClient<ITmdbClient, TmdbClient>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TmdbOptions>>().Value;
    httpClient.BaseAddress = new Uri(options.BaseAddress);
    httpClient.Timeout = TimeSpan.FromSeconds(20);
});

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

app.UseStatusCodePages();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.MapGet("/api/status", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/search", async ([FromQuery] string query, ITmdbClient tmdbClient, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(query))
    {
        return Results.BadRequest(new { message = "Query is required." });
    }

    try
    {
        var results = await tmdbClient.SearchAsync(query, cancellationToken);
        return Results.Ok(results);
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

app.MapGet("/api/search/preview", async ([FromQuery] int tmdbId, [FromQuery] MediaType mediaType, ITmdbClient tmdbClient, CancellationToken cancellationToken) =>
{
    if (tmdbId <= 0)
    {
        return Results.BadRequest(new { message = "A valid TMDB id is required." });
    }

    try
    {
        var now = DateTime.UtcNow;
        MediaDetailDto? payload = null;

        switch (mediaType)
        {
            case MediaType.Movie:
                var movie = await tmdbClient.GetMovieDetailsAsync(tmdbId, cancellationToken);
                if (movie is null)
                {
                    return Results.Problem("Unable to retrieve movie details from TMDB.", statusCode: StatusCodes.Status502BadGateway);
                }
                payload = CreatePreviewFromMovie(movie, now);
                break;

            case MediaType.TvShow:
                var tv = await tmdbClient.GetTvDetailsAsync(tmdbId, cancellationToken);
                if (tv is null)
                {
                    return Results.Problem("Unable to retrieve TV show details from TMDB.", statusCode: StatusCodes.Status502BadGateway);
                }
                payload = CreatePreviewFromTvShow(tv, now);
                break;
        }

        return payload is null ? Results.BadRequest() : Results.Ok(payload);
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

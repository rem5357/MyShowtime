using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyShowtime.Api.Data;
using MyShowtime.Api.Entities;
using MyShowtime.Api.Mappings;
using MyShowtime.Api.Options;
using MyShowtime.Api.Services;
using MyShowtime.Shared.Dtos;
using MyShowtime.Shared.Requests;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();

builder.Services.Configure<TmdbOptions>(builder.Configuration.GetSection(TmdbOptions.SectionName));

builder.Services.AddHttpClient<ITmdbClient, TmdbClient>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TmdbOptions>>().Value;
    httpClient.BaseAddress = new Uri(options.BaseAddress);
    httpClient.Timeout = TimeSpan.FromSeconds(15);
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

app.MapGet("/api/search", async (
    [FromQuery] string query,
    ITmdbClient tmdbClient,
    CancellationToken cancellationToken) =>
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

app.MapGet("/api/shows", async (ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    var shows = await db.Shows
        .AsNoTracking()
        .OrderByDescending(s => s.CreatedAtUtc)
        .ToListAsync(cancellationToken);

    return Results.Ok(shows.Select(show => show.ToDto()));
});

app.MapGet("/api/shows/{id:guid}", async (Guid id, ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    var show = await db.Shows.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    return show is null ? Results.NotFound() : Results.Ok(show.ToDto());
});

app.MapGet("/api/shows/tmdb/{tmdbId:int}", async (int tmdbId, ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    var show = await db.Shows.AsNoTracking().FirstOrDefaultAsync(s => s.TmdbId == tmdbId, cancellationToken);
    return show is null ? Results.NotFound() : Results.Ok(show.ToDto());
});

app.MapPost("/api/shows", async (
    [FromBody] SaveShowRequest request,
    ApplicationDbContext db,
    CancellationToken cancellationToken) =>
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

    var existing = await db.Shows.FirstOrDefaultAsync(s => s.TmdbId == request.TmdbId, cancellationToken);
    var releaseDate = ParseReleaseDate(request.ReleaseDate);

    if (existing is null)
    {
        var entity = new Show
        {
            TmdbId = request.TmdbId,
            Title = request.Title.Trim(),
            MediaType = NormalizeMediaType(request.MediaType),
            Overview = request.Overview,
            PosterPath = request.PosterPath,
            ReleaseDate = releaseDate,
            Notes = request.Notes,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Shows.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/shows/{entity.Id}", entity.ToDto());
    }

    existing.Title = request.Title.Trim();
    existing.MediaType = NormalizeMediaType(request.MediaType);
    existing.Overview = request.Overview;
    existing.PosterPath = request.PosterPath;
    existing.ReleaseDate = releaseDate;
    existing.Notes = request.Notes;
    existing.UpdatedAtUtc = DateTime.UtcNow;

    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(existing.ToDto());
});

app.MapDelete("/api/shows/{id:guid}", async (Guid id, ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    var entity = await db.Shows.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    if (entity is null)
    {
        return Results.NotFound();
    }

    db.Shows.Remove(entity);
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

app.MapPost("/api/shows/{tmdbId:int}/refresh", async (
    int tmdbId,
    [FromQuery] string? mediaType,
    ApplicationDbContext db,
    ITmdbClient tmdbClient,
    CancellationToken cancellationToken) =>
{
    var entity = await db.Shows.FirstOrDefaultAsync(s => s.TmdbId == tmdbId, cancellationToken);
    if (entity is null)
    {
        return Results.NotFound(new { message = "Show not found in the local catalog." });
    }

    try
    {
        var details = await tmdbClient.GetDetailsAsync(tmdbId, mediaType ?? entity.MediaType, cancellationToken);
        if (details is null)
        {
            return Results.Problem("Unable to fetch details from TMDB at this time.", statusCode: StatusCodes.Status502BadGateway);
        }

        entity.Title = details.Title ?? details.Name ?? entity.Title;
        entity.Overview = details.Overview ?? entity.Overview;
        entity.PosterPath = details.PosterPath ?? entity.PosterPath;
        entity.MediaType = NormalizeMediaType(mediaType ?? details.MediaType ?? entity.MediaType);
        entity.ReleaseDate = ParseReleaseDate(details.ReleaseDate ?? details.FirstAirDate);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(entity.ToDto());
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

app.Run();

static DateOnly? ParseReleaseDate(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    return DateOnly.TryParse(value, out var date) ? date : null;
}

static string NormalizeMediaType(string? mediaType)
{
    if (string.IsNullOrWhiteSpace(mediaType))
    {
        return "movie";
    }

    var normalized = mediaType.Trim().ToLowerInvariant();
    return normalized is "movie" or "tv" ? normalized : "movie";
}

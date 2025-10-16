using System.Collections.Generic;

namespace MyShowtime.Api.Services.Models;

public sealed class PersonMovieAggregation
{
    private readonly HashSet<string> _contributors = new(StringComparer.OrdinalIgnoreCase);

    public PersonMovieAggregation(int id, string title)
    {
        Id = id;
        Title = title;
    }

    public int Id { get; }
    public string Title { get; }
    public string? ReleaseDate { get; private set; }
    public string? Overview { get; private set; }
    public string? PosterPath { get; private set; }
    public double Popularity { get; private set; }

    public IReadOnlyCollection<string> Contributors => _contributors;

    public void Update(string? releaseDate, string? overview, string? posterPath, double popularity)
    {
        if (string.IsNullOrWhiteSpace(ReleaseDate) && !string.IsNullOrWhiteSpace(releaseDate))
        {
            ReleaseDate = releaseDate;
        }

        if (string.IsNullOrWhiteSpace(Overview) && !string.IsNullOrWhiteSpace(overview))
        {
            Overview = overview;
        }

        if (string.IsNullOrWhiteSpace(PosterPath) && !string.IsNullOrWhiteSpace(posterPath))
        {
            PosterPath = posterPath;
        }

        if (popularity > Popularity)
        {
            Popularity = popularity;
        }
    }

    public void AddContribution(string personName, string? role)
    {
        var contribution = string.IsNullOrWhiteSpace(role)
            ? personName
            : $"{personName} ({role})";

        _contributors.Add(contribution);
    }
}

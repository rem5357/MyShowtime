namespace MyShowtime.Api.Options;

public class TmdbOptions
{
    public const string SectionName = "Tmdb";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseAddress { get; set; } = "https://api.themoviedb.org/3/";
}

namespace MyShowtime.Api.Options;

public class TmdbOptions
{
    public const string SectionName = "Tmdb";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseAddress { get; set; } = "https://api.themoviedb.org/3/";
    public string DefaultLanguage { get; set; } = "en-US";
    public string DefaultRegion { get; set; } = "US";
}

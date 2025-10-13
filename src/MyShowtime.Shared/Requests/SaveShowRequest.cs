using System.ComponentModel.DataAnnotations;

namespace MyShowtime.Shared.Requests;

public class SaveShowRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int TmdbId { get; set; }

    [Required]
    [MaxLength(512)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(32)]
    public string MediaType { get; set; } = "movie";

    public string? Overview { get; set; }

    [MaxLength(256)]
    public string? PosterPath { get; set; }

    public string? ReleaseDate { get; set; }

    [MaxLength(1024)]
    public string? Notes { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace MyShowtime.Api.Entities;

public class Show
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Range(1, int.MaxValue)]
    public int TmdbId { get; set; }

    [Required]
    [MaxLength(512)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(32)]
    public string MediaType { get; set; } = "movie";

    public DateOnly? ReleaseDate { get; set; }

    public string? Overview { get; set; }

    [MaxLength(256)]
    public string? PosterPath { get; set; }

    [MaxLength(1024)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}

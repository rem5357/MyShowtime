using System.ComponentModel.DataAnnotations;
using MyShowtime.Shared.Enums;

namespace MyShowtime.Api.Entities;

public class Episode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MediaId { get; set; }

    public Media Media { get; set; } = null!;

    [Range(0, int.MaxValue)]
    public int TmdbEpisodeId { get; set; }

    [Range(0, 99)]
    public int SeasonNumber { get; set; }

    [Range(0, 999)]
    public int EpisodeNumber { get; set; }

    [Required]
    [MaxLength(512)]
    public string Title { get; set; } = string.Empty;

    public DateOnly? AirDate { get; set; }

    public bool IsSpecial { get; set; }

    public string? Synopsis { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}

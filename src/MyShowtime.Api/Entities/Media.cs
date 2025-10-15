using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyShowtime.Shared.Enums;

namespace MyShowtime.Api.Entities;

public class Media
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Range(1, int.MaxValue)]
    public int TmdbId { get; set; }

    public MediaType MediaType { get; set; }

    [Required]
    [MaxLength(512)]
    public string Title { get; set; } = string.Empty;

    public DateOnly? ReleaseDate { get; set; }

    [Range(0, 10)]
    public int Priority { get; set; } = 3;

    [MaxLength(64)]
    public string? Source { get; set; }

    [MaxLength(64)]
    public string? AvailableOn { get; set; }

    public ViewState WatchState { get; set; } = ViewState.Unwatched;

    public bool Hidden { get; set; }

    public string? Synopsis { get; set; }

    [MaxLength(256)]
    public string? PosterPath { get; set; }

    [Column(TypeName = "text")]
    public string? Genres { get; set; }

    [Column(TypeName = "text")]
    public string? Cast { get; set; }

    [Column(TypeName = "text")]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? LastSyncedAtUtc { get; set; }

    public ICollection<Episode> Episodes { get; set; } = new List<Episode>();
}

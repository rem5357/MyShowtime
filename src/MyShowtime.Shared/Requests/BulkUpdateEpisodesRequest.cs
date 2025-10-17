using System.ComponentModel.DataAnnotations;
using MyShowtime.Shared.Enums;

namespace MyShowtime.Shared.Requests;

public class BulkUpdateEpisodesRequest
{
    [Required]
    public ViewState WatchState { get; set; }

    /// <summary>
    /// If true, only updates episodes with SeasonNumber >= 1 (excludes specials/Season 0).
    /// If false, updates all episodes regardless of season.
    /// </summary>
    public bool ExcludeSpecials { get; set; }
}

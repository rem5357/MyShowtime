using MyShowtime.Shared.Enums;

namespace MyShowtime.Api.Entities;

/// <summary>
/// Junction table linking users to episodes with their watch state.
/// </summary>
public class UserEpisode
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public Guid EpisodeId { get; set; }
    public ViewState WatchState { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public AppUser User { get; set; } = null!;
    public Episode Episode { get; set; } = null!;
}

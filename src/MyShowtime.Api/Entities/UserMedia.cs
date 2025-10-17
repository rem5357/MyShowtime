using MyShowtime.Shared.Enums;

namespace MyShowtime.Api.Entities;

/// <summary>
/// Junction table linking users to media with their personal tracking data.
/// </summary>
public class UserMedia
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public Guid MediaId { get; set; }
    public ViewState WatchState { get; set; }
    public int Priority { get; set; } = 3;
    public bool Hidden { get; set; }
    public string? Notes { get; set; }
    public string? Source { get; set; }
    public string? AvailableOn { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public AppUser User { get; set; } = null!;
    public Media Media { get; set; } = null!;
}

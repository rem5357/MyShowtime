namespace MyShowtime.Api.Entities;

/// <summary>
/// User preferences and application settings.
/// </summary>
public class UserSettings
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public Guid? LastSelectedMediaId { get; set; }
    public string? Preferences { get; set; } // JSONB for theme, etc.
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public AppUser User { get; set; } = null!;
    public Media? LastSelectedMedia { get; set; }
}

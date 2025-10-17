namespace MyShowtime.Api.Entities;

public class AppUser
{
    public int Id { get; set; }
    public Guid CognitoSub { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? CognitoUsername { get; set; }
    public string[]? Roles { get; set; }
    public DateTime LastLogin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    public string? Metadata { get; set; } // Will be stored as JSONB
}

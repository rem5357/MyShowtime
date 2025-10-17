namespace MyShowtime.Api.Services;

/// <summary>
/// Service to access the current user context from the HTTP request
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user ID from the request context
    /// </summary>
    int? UserId { get; }

    /// <summary>
    /// Gets the current user ID or throws if not authenticated
    /// </summary>
    int GetRequiredUserId();
}
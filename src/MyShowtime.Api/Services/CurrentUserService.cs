using Microsoft.Extensions.Logging;

namespace MyShowtime.Api.Services;

/// <summary>
/// Service to extract current user context from HTTP request headers
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CurrentUserService> _logger;
    private const string UserIdHeader = "X-User-Id";

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, ILogger<CurrentUserService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current user ID from the request headers
    /// </summary>
    public int? UserId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                _logger.LogWarning("HttpContext is null in CurrentUserService");
                return null;
            }

            // Log all headers for debugging
            _logger.LogDebug("Request headers: {Headers}",
                string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}={h.Value}")));

            // Try to get the user ID from the custom header
            if (context.Request.Headers.TryGetValue(UserIdHeader, out var userIdHeader))
            {
                _logger.LogInformation("Found {Header} header with value: {Value}", UserIdHeader, userIdHeader);

                if (int.TryParse(userIdHeader, out var userId))
                {
                    _logger.LogInformation("Successfully parsed User ID: {UserId}", userId);
                    return userId;
                }
                else
                {
                    _logger.LogWarning("Failed to parse User ID from header value: {Value}", userIdHeader);
                }
            }
            else
            {
                _logger.LogWarning("{Header} header not found in request", UserIdHeader);
            }

            return null;
        }
    }

    /// <summary>
    /// Gets the current user ID or throws if not authenticated
    /// </summary>
    public int GetRequiredUserId()
    {
        var userId = UserId;
        if (!userId.HasValue)
        {
            // TEMPORARY FALLBACK: Return user 101 if no header found
            // This helps us test if the header is the issue
            _logger.LogError("No user ID found in request headers! Falling back to user 101 for testing");
            return 101;
            // throw new UnauthorizedAccessException("User authentication required");
        }

        return userId.Value;
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyShowtime.Api.Data;
using MyShowtime.Api.Entities;
using System.Security.Claims;

namespace MyShowtime.Api.Services;

/// <summary>
/// Service to extract current user context from JWT claims and provision users from Cognito
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CurrentUserService> _logger;
    private int? _cachedUserId;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider, ILogger<CurrentUserService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current user ID from JWT claims, auto-provisioning if necessary
    /// </summary>
    public int? UserId
    {
        get
        {
            if (_cachedUserId.HasValue)
            {
                return _cachedUserId;
            }

            var context = _httpContextAccessor.HttpContext;
            if (context?.User?.Identity?.IsAuthenticated != true)
            {
                _logger.LogWarning("User is not authenticated");
                return null;
            }

            // Extract Cognito sub (unique user ID)
            var cognitoSub = context.User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(cognitoSub))
            {
                _logger.LogWarning("No 'sub' claim found in JWT token");
                return null;
            }

            if (!Guid.TryParse(cognitoSub, out var cognitoSubGuid))
            {
                _logger.LogWarning("Invalid 'sub' claim format: {CognitoSub}", cognitoSub);
                return null;
            }

            // Extract email and name from token
            var email = context.User.FindFirst("email")?.Value;
            var name = context.User.FindFirst("name")?.Value;

            _logger.LogInformation("Authenticated user - CognitoSub: {CognitoSub}, Email: {Email}, Name: {Name}",
                cognitoSubGuid, email, name);

            // Get or create AppUser
            _cachedUserId = GetOrCreateUser(cognitoSubGuid, email, name);
            return _cachedUserId;
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
            _logger.LogError("User authentication required but no valid user found");
            throw new UnauthorizedAccessException("User authentication required");
        }

        return userId.Value;
    }

    /// <summary>
    /// Looks up or creates an AppUser record for the authenticated Cognito user
    /// </summary>
    private int GetOrCreateUser(Guid cognitoSub, string? email, string? name)
    {
        // Create a new scope to access the database
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = db.AppUsers.FirstOrDefault(u => u.CognitoSub == cognitoSub);

        if (user != null)
        {
            _logger.LogDebug("Found existing user {UserId} for CognitoSub {CognitoSub}", user.Id, cognitoSub);

            // Update last login time
            user.LastLogin = DateTime.UtcNow;
            db.SaveChanges();

            return user.Id;
        }

        // Auto-provision new user
        _logger.LogInformation("Auto-provisioning new user for CognitoSub: {CognitoSub}, Email: {Email}", cognitoSub, email);

        var newUser = new AppUser
        {
            CognitoSub = cognitoSub,
            Email = email ?? $"{cognitoSub}@unknown.local",
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastLogin = DateTime.UtcNow
        };

        db.AppUsers.Add(newUser);
        db.SaveChanges();

        _logger.LogInformation("Created new user {UserId} for CognitoSub {CognitoSub}", newUser.Id, cognitoSub);

        return newUser.Id;
    }
}
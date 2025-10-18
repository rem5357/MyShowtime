# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MyShowtime is a Blazor WebAssembly media library companion application that tracks movies and TV shows. It integrates with The Movie Database (TMDB) API to enable users to search, import, and manage their personal media collection with detailed metadata, watch states, and episode tracking.

**Current Status**: Live service at `http://myshowtime.local` and `http://100.102.6.85/MyShowtime/` (Tailscale), with PostgreSQL backend and Nginx reverse proxy.

## Architecture

Three-tier architecture with three .NET 8.0 projects:

1. **MyShowtime.Api** - ASP.NET Core Minimal API backend
   - PostgreSQL database via Entity Framework Core 9.0
   - TMDB API integration with Polly retry policies
   - Endpoints for library management, search, import, and sync

2. **MyShowtime.Client** - Blazor WebAssembly frontend
   - Four-panel dashboard layout (library, search, details, episodes)
   - Client-side caching and localStorage integration
   - Direct HTTP calls to API endpoints

3. **MyShowtime.Shared** - Shared DTOs, enums, and request models
   - `MediaType` enum: Movie, TvShow
   - `ViewState` enum: Unwatched (0), Partial (1), Watched (2)
   - DTOs for media, episodes, search results

## Common Commands

### Building and Running

```bash
# Build the entire solution
dotnet build MyShowtime.sln

# Build specific projects
dotnet build src/MyShowtime.Api/MyShowtime.Api.csproj
dotnet build src/MyShowtime.Client/MyShowtime.Client.csproj

# Run API locally (development)
dotnet run --project src/MyShowtime.Api/MyShowtime.Api.csproj

# Run Client locally (requires API running)
dotnet run --project src/MyShowtime.Client/MyShowtime.Client.csproj

# Publish for production
dotnet publish src/MyShowtime.Api/MyShowtime.Api.csproj -c Release -o /tmp/api-output
dotnet publish src/MyShowtime.Client/MyShowtime.Client.csproj -c Release -o /tmp/client-output
```

### Deployment

```bash
# Automated production build and deployment
# Auto-increments BuildInfo.BuildNumber, publishes both projects, rsyncs to /var/www/projects/MyShowtime/
./scripts/build_and_deploy.sh
```

The deployment script:
- Increments `BuildInfo.BuildNumber` in `src/MyShowtime.Client/BuildInfo.cs`
- Publishes API to Release and deploys to `/var/www/projects/MyShowtime/api/`
- Publishes Client to Release and deploys to `/var/www/projects/MyShowtime/wwwroot/`

**Important**: Always use the deployment script for production builds to keep the build badge in sync.

### Database Migrations

```bash
# Create a new migration
dotnet ef migrations add <MigrationName> --project src/MyShowtime.Api

# Apply migrations to database
dotnet ef database update --project src/MyShowtime.Api

# Rollback to a specific migration
dotnet ef database update <MigrationName> --project src/MyShowtime.Api
```

## Key API Endpoints

All endpoints use `/api/` prefix:

**Health & Status**
- `GET /api/status` - Service health check

**TMDB Search & Discovery**
- `GET /api/tmdb/search?query={query}&page={page}&mediaType={type}` - Search TMDB (cached, 10min TTL)
- `GET /api/tmdb/details?tmdbId={id}&mediaType={type}` - Get full TMDB preview before import

**Media Library**
- `GET /api/media?includeHidden={bool}` - List all library items
- `GET /api/media/{id}` - Get media details
- `GET /api/media/{id}/episodes` - Get TV show episodes
- `POST /api/media/import` - Add media from TMDB (body: `ImportMediaRequest`)
- `POST /api/media/{id}/sync` - Refresh metadata from TMDB
- `PUT /api/media/{id}` - Update metadata (body: `UpdateMediaRequest`)
- `PUT /api/media/{mediaId}/episodes/{episodeId}/viewstate` - Update episode watch state

## Data Models

### Core Entities

**Media** (`src/MyShowtime.Api/Entities/Media.cs`):
- Stores movies and TV shows with TMDB metadata (shared across all users)
- One-to-many relationship with Episodes
- Unique constraint on `TmdbId`
- Genres and Cast stored as JSON-backed text fields

**Episode** (`src/MyShowtime.Api/Entities/Episode.cs`):
- TV show episodes with season/episode numbers (shared across all users)
- Foreign key to Media
- Unique constraint on `MediaId + SeasonNumber + EpisodeNumber`

**AppUser** (`src/MyShowtime.Api/Entities/AppUser.cs`):
- User account information
- Primary key `Id` (integer), unique `CognitoSub` (Guid)
- Stores email, name, roles, metadata (JSONB)

**UserMedia** (`src/MyShowtime.Api/Entities/UserMedia.cs`):
- Junction table for user-specific media tracking
- Composite unique constraint on `UserId + MediaId`
- Stores user-specific: WatchState, Priority, Hidden, Notes, Source, AvailableOn
- Foreign keys to AppUser and Media (cascade delete)

**UserEpisode** (`src/MyShowtime.Api/Entities/UserEpisode.cs`):
- Junction table for user-specific episode watch states
- Composite unique constraint on `UserId + EpisodeId`
- Stores per-user episode WatchState
- Foreign keys to AppUser and Episode (cascade delete)

**UserSettings** (`src/MyShowtime.Api/Entities/UserSettings.cs`):
- One-to-one with AppUser (unique constraint on UserId)
- Stores LastSelectedMediaId and Preferences (JSONB)

### Key DTOs

Located in `src/MyShowtime.Shared/Dtos/`:
- `MediaSummaryDto` - Library list representation
- `MediaDetailDto` - Complete media information
- `EpisodeDto` - Episode-level details
- `TmdbSearchResponseDto` - Search results wrapper
- `TmdbSearchItemDto` - Individual search result

## External API Integration

### TMDB API Configuration

Configuration in `src/MyShowtime.Api/Options/TmdbOptions.cs`:
- API key loaded from environment variable
- Base URL: `https://api.themoviedb.org/3/`
- Language and region settings

Service implementation: `src/MyShowtime.Api/Services/TmdbClient.cs`

**Resilience**: Polly retry policy with 3 retries, exponential backoff (250ms base × 2^n) + jitter for rate limiting (429) and 5xx errors.

**Caching Strategy**:
- Server-side: MemoryCache for TMDB search results (10-minute TTL, max 200 entries)
- Client-side: Component-level search cache (10-entry LRU)
- Watch provider lookups cached for 6 hours per media ID
- ETag-based conditional requests (304 Not Modified)

**Concurrency**: SemaphoreSlim (limit=3) throttles watch provider enrichment calls.

## UI Architecture

### Main Dashboard Layout

Located in `src/MyShowtime.Client/Pages/Home.razor` (~1500 lines):

**Four-panel layout**:
1. **Upper-Left**: Local media library with filtering (watch state) and sorting
2. **Lower-Left**: TMDB search with media/people scope toggle, pagination
3. **Center**: Media details (poster, metadata, genres, cast, priority/watch controls, notes)
4. **Right**: Episode browser for TV shows with per-episode watch state

**State Management**:
- Component-level state for library, search results, selected items, episodes
- Browser localStorage for last-selected media persistence
- Client-side search caching (LRU, 10-entry limit)

### Service Layer

`src/MyShowtime.Client/Services/MediaLibraryService.cs`:
- HTTP abstraction for all API calls
- Async methods with proper error handling
- Response deserialization via System.Text.Json

## Important Implementation Details

### Database Schema

Four migrations applied:
1. `InitialCreate` - Initial schema
2. `CreateMediaSchema` - Media and Episodes tables with constraints
3. `AddAppUserTable` - AppUser table for user accounts
4. `AddMultiUserArchitecture` - UserMedia, UserEpisode, UserSettings tables; migrated existing data to user 101

**Unique Constraints**:
- Media: `TmdbId` must be unique
- Episodes: `MediaId + SeasonNumber + EpisodeNumber` must be unique
- AppUser: `CognitoSub` must be unique
- UserMedia: `UserId + MediaId` must be unique (composite)
- UserEpisode: `UserId + EpisodeId` must be unique (composite)
- UserSettings: `UserId` must be unique (one-to-one with AppUser)

### TMDB Import Flow

1. User searches via `/api/tmdb/search`
2. Optional per-result enrichment with watch provider data
3. User selects result → preview via `/api/tmdb/details`
4. "Add to Tracking" → `/api/media/import` creates library entry
5. Import fetches full details, aggregates episodes (for TV)
6. Persisted to PostgreSQL
7. Subsequent refreshes via `/api/media/{id}/sync`

### Deployment Topology

```
[Browser Client (Blazor WASM)]
       ↓ HTTP/Tailscale
    [Nginx Reverse Proxy]
       ↓ (localhost proxy to port 5000)
[ASP.NET Core API]
       ↓
  [PostgreSQL Database]
       ↓ (HTTPS)
   [TMDB API v3]
```

## Configuration & Environment

**Configuration Files**:
- `src/MyShowtime.Api/appsettings.json` - Base configuration
- `src/MyShowtime.Api/appsettings.Development.json` - Dev overrides
- `src/MyShowtime.Api/appsettings.Production.json` - Production overrides (if present)

**Environment Variables**:
- Located in `/etc/myshowtime/myshowtime.env` (external, not in repo)
- Contains: TMDB API key, PostgreSQL credentials, ASP.NET Core environment

**Nginx Configuration**: `/etc/nginx/snippets/projects/MyShowtime.conf`

## Operational Notes

### Deployment Paths

- Client assets: `/var/www/projects/MyShowtime/wwwroot/`
- API binaries: `/var/www/projects/MyShowtime/api/`
- Nginx config: `/etc/nginx/snippets/projects/MyShowtime.conf`

### Alternative Client Publish Method

If needed, you can publish the client manually:
```bash
dotnet publish -c Release src/MyShowtime.Client -o /tmp/<guid>
rsync -a /tmp/<guid>/ /var/www/projects/MyShowtime/wwwroot/
```

Note: The publish output nests a `wwwroot` directory—rsync into the target and delete the extra `/wwwroot` folder afterwards.

### Post-Deployment

After deploying, **always perform a hard refresh** in the browser (Ctrl+F5 or Cmd+Shift+R) - Blazor WASM service worker caching can hold stale assets and mask layout changes. Always redeploy the Blazor client after meaningful UI/API changes for immediate verification.

### Screenshot Review

When reviewing screenshots, check `/home/mithroll/Dropbox/Screenshots/` for the most recent files.

### Base URL Configuration

When hosting under `/MyShowtime/`, keep `<base href="./" />` in both repo and deployed `wwwroot/index.html`. Republish with `dotnet publish -c Release src/MyShowtime.Client` to keep deployed files in sync.

Preserve `/etc/nginx/snippets/projects/MyShowtime.conf` (rewrites + proxy) whenever nginx configs are refreshed so the scoped path keeps working.

## Known Limitations

- No automated tests (unit/integration/E2E)
- HTTPS not configured (localhost only)
- Secrets stored in environment file (rotation recommended)
- Sequential TMDB imports for seasons (potential performance bottleneck)
- Watch provider lookup adds per-result latency
- UI font sizing needs refinement

## Suggested Improvements

When working on enhancements:

1. **Advanced features** still pending: person/crew search mode, streaming availability editing, richer relationship views
2. **Testing**: Add unit tests for services, integration tests for API endpoints, E2E tests for critical workflows
3. **Security**: Enable HTTPS, rotate credentials, implement proper secret management
4. **Performance**: Consider caching/batching watch provider lookups, parallel TMDB requests for seasons
5. **User Experience**: Refine font sizing, improve mobile responsiveness

## Code Style & Conventions

- Target: .NET 8.0 across all projects
- C# 12 language features enabled
- Nullable reference types enabled
- Implicit usings enabled
- Standard REST conventions for API endpoints
- JSON serialization via System.Text.Json
- Async/await throughout with CancellationToken support
- ProblemDetails for error responses (500, 400, 404, 502)

## Implementation Patterns & Best Practices

### Blazor Client State Management

- Use `localStorage` helpers (via `myShowtimeState` JS interop) for user-specific state like last-selected media
- Keep state per-browser without introducing server-side user tracking
- **Always wrap JS interop calls with guards** to prevent "Unhandled error" messages when scripts haven't loaded yet

### UI Responsiveness

- Dashboard panels are height-capped (library/search at 50vh, details/episodes at viewport height) with internal scrolling
- Grid layout uses 40/30/30 split to keep library panel dominant while leaving room for preview and episode panes
- Dense tabular layouts benefit from ~40% reduced row padding; tweak font + padding together to avoid cramped hit targets
- **Use `StateHasChanged()` or `InvokeAsync()`** when flipping between library selections and search previews to trigger UI refreshes during TMDB calls for instant-feeling interactions

### UI Features

- Zoom guidance uses a dismissable in-app banner with timestamp stored in localStorage, reappearing after ~18 hours
- For TV shows without episodes selected, show movie posters in the episode column
- Search panel rows reflect provider availability when present
- Episode browser shows per-season episodes with per-episode watch state radios

### Performance Optimization

- **Watch Provider Enrichment**: Use parallel async processing with `Task.WhenAll()` for enriching search results with streaming provider data. The semaphore limits concurrent TMDB calls (3) while allowing multiple items to be processed simultaneously. **Never use sequential `foreach` loops with `await` inside** - this creates bottlenecks and timeouts.

### Authentication (AWS Cognito - v0.70)

**Architecture**: Production-ready authentication using AWS Cognito User Pools with JWT bearer tokens and auto-user provisioning:
- OAuth 2.0 / OpenID Connect (OIDC) authentication flow
- Authorization Code Grant with PKCE (secure for public clients like Blazor WASM)
- JWT bearer tokens validated on every API request
- User records auto-provisioned on first login from JWT claims
- Multi-user data isolation with shared media catalog and per-user tracking

**AWS Cognito Configuration**:
- **User Pool**: `us-east-2_VkwlcR2m8` (AWS region: us-east-2 - Ohio)
- **App Client ID**: `548ed6mlir2cdkq30aphbn3had`
- **Cognito Domain**: `https://us-east-2vkwlcr2m8.auth.us-east-2.amazoncognito.com`
- **Authority**: `https://cognito-idp.us-east-2.amazonaws.com/us-east-2_VkwlcR2m8`
- **OIDC Metadata**: `https://cognito-idp.us-east-2.amazonaws.com/us-east-2_VkwlcR2m8/.well-known/openid-configuration`
- **Scopes**: openid, email, profile
- **Callback URL**: `https://goldshire.tail80a7ec.ts.net/MyShowtime/authentication/login-callback`
- **Post-logout URL**: `https://goldshire.tail80a7ec.ts.net/MyShowtime/`

**Client-Side Implementation** (`src/MyShowtime.Client/`):

1. **NuGet Packages**:
   - `Microsoft.AspNetCore.Components.WebAssembly.Authentication` (v8.0.21)
   - `Microsoft.Extensions.Http` (v8.0.1)

2. **Program.cs**:
   - Configures OIDC authentication with `AddOidcAuthentication()`
   - Sets up named HttpClient factory with `BaseAddressAuthorizationMessageHandler` for automatic JWT injection
   - HttpClient configuration:
     ```csharp
     builder.Services.AddHttpClient("MyShowtime.Api", client =>
         client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
         .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

     builder.Services.AddScoped(sp =>
         sp.GetRequiredService<IHttpClientFactory>().CreateClient("MyShowtime.Api"));
     ```

3. **App.razor**:
   - Wraps router with `<CascadingAuthenticationState>`
   - Uses `<AuthorizeRouteView>` to protect routes
   - Provides `<Authorizing>` and `<NotAuthorized>` UI states

4. **Authentication.razor** (`/authentication/{action}`):
   - OAuth callback handler page
   - Uses `<RemoteAuthenticatorView>` with templates for login/logout states
   - Handles: LoggingIn, CompletingLogOut, LogOutSucceeded, LogOutFailed

5. **MainLayout.razor**:
   - Uses `<AuthorizeView>` to display authenticated user info
   - Shows user name from JWT claims: `@context.User.Identity?.Name`
   - Logout button navigates to `authentication/logout`

6. **Home.razor**:
   - Protected with `@attribute [Authorize]` (requires authentication)

7. **_Imports.razor**:
   - Imports `Microsoft.AspNetCore.Components.Authorization`
   - Imports `Microsoft.AspNetCore.Components.WebAssembly.Authentication`

**Server-Side Implementation** (`src/MyShowtime.Api/`):

1. **NuGet Packages**:
   - `Microsoft.AspNetCore.Authentication.JwtBearer` (v8.0.21)

2. **Program.cs**:
   - Registers `IHttpContextAccessor` for accessing HTTP context
   - Configures JWT Bearer authentication:
     ```csharp
     builder.Services.AddAuthentication("Bearer")
         .AddJwtBearer(options =>
         {
             options.Authority = "https://cognito-idp.us-east-2.amazonaws.com/us-east-2_VkwlcR2m8";
             options.TokenValidationParameters = new TokenValidationParameters
             {
                 ValidateIssuer = true,
                 ValidIssuer = "https://cognito-idp.us-east-2.amazonaws.com/us-east-2_VkwlcR2m8",
                 ValidateAudience = true,
                 ValidAudience = "548ed6mlir2cdkq30aphbn3had",
                 ValidateLifetime = true
             };
             options.MapInboundClaims = false; // Keep original claim names like "sub"
         });
     ```
   - Adds authentication/authorization middleware (MUST be after routing, before endpoints)

3. **CurrentUserService.cs** (`src/MyShowtime.Api/Services/CurrentUserService.cs`):
   - **Complete rewrite** for JWT-based authentication
   - Extracts user from JWT claims instead of HTTP headers
   - Key JWT claims:
     - `sub` (Subject): Cognito user ID (Guid) - stored as `CognitoSub` in AppUser table
     - `email`: User's email address
     - `name`: User's display name
   - **Auto-provisioning logic**:
     ```csharp
     // Extracts CognitoSub from JWT "sub" claim
     var cognitoSub = context.User.FindFirst("sub")?.Value;

     // Looks up user by CognitoSub
     var user = db.AppUsers.FirstOrDefault(u => u.CognitoSub == cognitoSubGuid);

     // If not found, creates new user with Cognito claims
     if (user == null)
     {
         var newUser = new AppUser
         {
             CognitoSub = cognitoSubGuid,
             Email = email ?? $"{cognitoSub}@unknown.local",
             Name = name,
             IsActive = true,
             CreatedAt = DateTime.UtcNow,
             LastLogin = DateTime.UtcNow
         };
         db.AppUsers.Add(newUser);
         db.SaveChanges();
     }
     ```
   - Updates `LastLogin` timestamp on every authenticated request
   - Caches user ID per request to avoid multiple database lookups
   - Provides `GetRequiredUserId()` for endpoints requiring authentication

4. **All API Endpoints**:
   - Continue using `ICurrentUserService.GetRequiredUserId()` to get authenticated user
   - No changes needed to endpoint implementations (seamless upgrade from header-based auth)
   - JWT validation happens automatically in middleware before requests reach endpoints

**Authentication Flow**:

1. **Initial Access**: User visits app → redirected to Cognito hosted UI for login
2. **Login**: User authenticates with Cognito (username/password, MFA, social providers, etc.)
3. **Callback**: Cognito redirects back with authorization code → PKCE exchange for tokens
4. **Token Storage**: Blazor WASM stores access token in browser storage (handled automatically)
5. **API Requests**: Every HTTP request includes JWT bearer token in `Authorization` header
6. **Token Validation**: ASP.NET Core middleware validates JWT signature, issuer, audience, expiration
7. **User Extraction**: CurrentUserService extracts `sub` claim and looks up/creates AppUser
8. **Data Access**: API endpoint uses user ID to filter database queries for data isolation

**Multi-User Data Model** (unchanged from v0.65):
- `Media` and `Episodes` tables: Shared TMDB catalog (all users)
- `UserMedia` junction table: Per-user tracking (watch state, priority, notes, etc.)
- `UserEpisode` junction table: Per-user episode watch states
- `UserSettings` table: Per-user preferences (one-to-one with AppUser)
- All queries filter by `UserId` to ensure complete data isolation

**Key Benefits of Cognito Implementation**:
- ✅ Production-ready authentication with industry-standard OAuth 2.0/OIDC
- ✅ Managed user pool (no password storage, automatic security updates)
- ✅ JWT bearer tokens with cryptographic signature validation
- ✅ Token expiration and refresh handled automatically by Blazor WASM
- ✅ Auto-user provisioning (friends can self-register via Cognito)
- ✅ Support for MFA, social login, password policies (configured in Cognito)
- ✅ Works seamlessly with Tailscale (private network, no public firewall exposure)
- ✅ Minimal changes to API endpoints (same `ICurrentUserService` interface)

**Database Schema**:
- `AppUser.CognitoSub` (Guid, unique): Maps to JWT "sub" claim
- `AppUser.Email`, `AppUser.Name`: Populated from JWT claims on first login
- `AppUser.LastLogin`: Updated on every authenticated request

**Setup Guide**:
- See `CognitoSetupGuide.md` for step-by-step AWS Console instructions
- Includes User Pool creation, app client configuration, hosted UI setup

**Critical Implementation Notes**:
1. **MapInboundClaims = false**: Keeps original JWT claim names (`sub`, not `http://schemas.xmlsoap.org/...`)
2. **BaseAddressAuthorizationMessageHandler**: Automatically adds JWT to requests matching base address
3. **Named HttpClient**: Use factory pattern to ensure handler is properly configured
4. **PKCE Flow**: Authorization Code with PKCE is secure for public clients (no client secret needed)
5. **Middleware Order**: Authentication must come after `UseRouting()`, before `MapControllers()`/`MapEndpoints()`
6. **Scoped vs Singleton**: CurrentUserService is scoped (per-request), caches user ID within request

**Files to Reference**:
- Setup Guide: `CognitoSetupGuide.md`
- Database Schema: `DatabaseSchema.md`
- Client Config: `src/MyShowtime.Client/Program.cs`
- Client Auth UI: `src/MyShowtime.Client/Pages/Authentication.razor`
- Client Layout: `src/MyShowtime.Client/Layout/MainLayout.razor`
- Client Imports: `src/MyShowtime.Client/_Imports.razor`
- API Config: `src/MyShowtime.Api/Program.cs`
- User Service: `src/MyShowtime.Api/Services/CurrentUserService.cs`

**Removed Files** (from v0.65 test implementation):
- `src/MyShowtime.Client/Pages/Login.razor` (replaced with Cognito hosted UI)
- `src/MyShowtime.Client/Services/UserStateService.cs` (replaced with OIDC auth state)
- `src/MyShowtime.Client/Services/AuthenticatedHttpMessageHandler.cs` (replaced with BaseAddressAuthorizationMessageHandler)

### AWS Cognito Implementation Status (v0.71)

**CURRENT STATUS: Partially implemented but NOT FUNCTIONAL**

The AWS Cognito authentication implementation is in progress but encounters a critical blocker. The application loads, displays the login UI, and successfully redirects to Cognito's hosted UI, but fails during the OAuth token exchange with a 400 Bad Request error.

**Root Cause:**
The current User Pool and App Client configuration is incompatible with Blazor WebAssembly:
1. **App Client has a Client Secret**: The existing app client (`548ed6mlir2cdkq30aphbn3had`) was created with a client secret
2. **Blazor WASM is a Public Client**: Single-page applications run entirely in the browser and cannot securely store secrets
3. **Token Exchange Fails**: Cognito expects the client secret during token exchange, but Blazor WASM doesn't send it (correctly, as it shouldn't)
4. **User Pool Uses Phone Authentication**: Configured with phone number as the sign-in attribute instead of email
5. **SMS Not Configured**: Phone-based password reset requires AWS SNS setup (additional complexity and cost)

**Issues Encountered:**

1. **Missing AuthenticationService.js** (FIXED in Build 143)
   - Error: `Could not find 'AuthenticationService.init' ('AuthenticationService' was undefined)`
   - Root cause: `index.html` missing required script reference
   - Fix: Added `<script src="_content/Microsoft.AspNetCore.Components.WebAssembly.Authentication/AuthenticationService.js"></script>`
   - Location: `src/MyShowtime.Client/wwwroot/index.html:30`

2. **Redirect URI Mismatch** (FIXED in Build 144)
   - Error: Cognito returned `error=redirect_mismatch`
   - Root cause: String concatenation didn't handle trailing slashes correctly
   - Fix: Used `Uri` class for proper path construction: `new Uri(baseUri, "authentication/login-callback").ToString()`
   - Location: `src/MyShowtime.Client/Program.cs:29-31`

3. **Missing OAuth Scope**
   - Error: "There was an error signing in" with no details
   - Root cause: Code requested "profile" scope but app client only had "openid", "email", "phone" enabled
   - Fix: Enabled "profile" scope in Cognito app client OpenID Connect scopes configuration
   - Note: Scopes must be explicitly enabled in AWS Console → App clients → Edit → OpenID Connect scopes

4. **Client Secret Incompatibility** (BLOCKER - not yet resolved)
   - Error: `POST https://us-east-2vkwlcr2m8.auth.us-east-2.amazoncognito.com/oauth2/token 400 (Bad Request)`
   - Root cause: App client was created as "Traditional web application" which generates a client secret
   - Impact: OAuth token exchange fails because Cognito expects the secret but Blazor WASM doesn't (and shouldn't) send it
   - Resolution: Must create NEW app client as "Single-page application (SPA)" type (public client, no secret)
   - AWS Limitation: Cannot remove client secret from existing app client; must create new one

5. **Phone Number Authentication Challenges**
   - User pool configured with phone number as primary sign-in attribute
   - Password reset via SMS failed: "Invalid input: Could not reset password for the account"
   - Root cause: AWS SNS not configured for SMS delivery (requires additional setup and incurs costs)
   - AWS Limitation: Cannot change sign-in attributes after user pool creation
   - Resolution: Must create NEW user pool with email as sign-in attribute

**Lessons Learned:**

1. **Blazor WASM Authentication Requirements:**
   - MUST include `AuthenticationService.js` script in `index.html` (not auto-discovered)
   - MUST use `BaseAddressAuthorizationMessageHandler` for automatic JWT injection
   - MUST use HttpClient factory pattern (named client) to ensure handler is properly configured
   - MUST use `Uri` class for callback URL construction to handle trailing slashes correctly

2. **AWS Cognito App Client Types:**
   - **Traditional web application**: Server-side apps (generates client secret, NOT compatible with Blazor WASM)
   - **Single-page application (SPA)**: Browser-based apps like Blazor WASM (public client, NO client secret)
   - **Mobile app**: Native mobile apps
   - **Machine-to-machine**: API-to-API communication (uses client credentials flow)
   - **Critical**: Choose SPA type for Blazor WebAssembly; Traditional type will fail token exchange

3. **Cognito Configuration Immutability:**
   - **App Client Secret**: Cannot be removed after creation; must create new app client
   - **Sign-in Attributes**: Cannot be changed after user pool creation; must create new pool
   - **Domain**: Can be changed but affects all app clients
   - **Recommendation**: Plan carefully during initial setup; test with throwaway pools first

4. **OAuth Scope Management:**
   - Scopes requested by client code MUST be enabled in app client configuration
   - Default scopes vary by app client type
   - "profile" scope is NOT auto-enabled; must be manually selected
   - Mismatch causes generic "error signing in" with no specific error details

5. **Redirect URL Matching:**
   - AWS Cognito requires EXACT match (case-sensitive, trailing slashes matter)
   - Callback URL: `https://goldshire.tail80a7ec.ts.net/MyShowtime/authentication/login-callback` (NO trailing slash)
   - Sign-out URL: `https://goldshire.tail80a7ec.ts.net/MyShowtime/` (WITH trailing slash)
   - Inconsistency is confusing but required (callback is specific endpoint, sign-out is directory/base)

6. **Phone vs Email Authentication:**
   - Phone authentication requires AWS SNS configuration (additional complexity, cost ~$0.00645 per SMS)
   - Email authentication works out-of-the-box with Cognito's built-in email service (free tier: 50 emails/day)
   - SMS verification codes can fail silently if SNS is not properly configured
   - **Recommendation**: Use email for simple apps; phone only if MFA or region requirements demand it

7. **Debugging OAuth Errors:**
   - Browser console shows network errors (400, redirect_mismatch, etc.)
   - Cognito hosted UI errors are often generic ("There was an error signing in")
   - Check Network tab for actual OAuth/token endpoint responses
   - Verify app client configuration matches code (client ID, scopes, redirect URIs)
   - Test with direct Cognito hosted UI URL to isolate client vs server issues

8. **User Management:**
   - Cannot delete users from AWS Console if only selection checkbox is visible (must use list view)
   - Cannot set permanent passwords from Console (security restriction against admin access)
   - Can create users with admin-set temporary passwords that must be changed on first login
   - Can mark email as verified to skip email verification step

**Next Steps (for future session):**

1. **Create New User Pool** with email-based authentication:
   - Sign-in attribute: Email (not phone)
   - MFA: Optional or disabled (simpler for friends/family app)
   - Email delivery: Use Cognito default (no AWS SES setup needed)
   - Self-service sign-up: Enabled (friends can register themselves)

2. **Create SPA App Client** for new pool:
   - App type: Single-page application (PUBLIC client)
   - OAuth flows: Authorization code grant with PKCE
   - Scopes: openid, email, profile
   - Callback URL: `https://goldshire.tail80a7ec.ts.net/MyShowtime/authentication/login-callback`
   - Sign-out URL: `https://goldshire.tail80a7ec.ts.net/MyShowtime/`
   - Verify NO client secret is generated

3. **Update Application Code**:
   - Update User Pool ID in `src/MyShowtime.Client/Program.cs` (line 15)
   - Update App Client ID in `src/MyShowtime.Client/Program.cs` (line 16)
   - Update Authority URL in `src/MyShowtime.Api/Program.cs` (line 405, 410)
   - Update Audience in `src/MyShowtime.Api/Program.cs` (line 412)
   - Update OIDC Metadata URL in `src/MyShowtime.Client/Program.cs` (line 26)

4. **Test End-to-End Authentication**:
   - Hard refresh browser (Ctrl+F5) after deployment
   - Verify redirect to Cognito hosted UI
   - Create test user account with email/password
   - Verify successful login and redirect back to app
   - Verify user auto-provisioned in AppUsers table
   - Verify JWT token included in API requests
   - Verify API endpoints filter data by user ID

5. **Delete Old Resources** (optional cleanup):
   - Old app client: `548ed6mlir2cdkq30aphbn3had`
   - Old user pool: `us-east-2_VkwlcR2m8`
   - Note: Keep until new setup is verified working

**Reference Documentation:**
- Current incomplete implementation: v0.70 (Build 142-144)
- Version bump to v0.71 (Build 144) marks partially-implemented state
- See `CognitoSetupGuide.md` for original setup instructions (needs update for SPA client)

### Blazor Error UI

- **Critical**: Always include the `#blazor-error-ui` CSS styles in `app.css` with `display: none;` to hide the error banner by default
- Without these styles, the error UI div will be visible even when no errors occur
- The error UI is controlled by Blazor's error handling and will automatically show when unhandled exceptions occur

## Additional Resources

See `CodexStatus.md` for:
- Detailed project status and completed features
- Lessons learned from recent development
- Outstanding considerations and next steps
- Operational notes and deployment details

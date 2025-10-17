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
- Multi-user authentication uses simple X-User-Id header (not production-ready; needs JWT/OAuth)

## Suggested Improvements

When working on enhancements:

1. **Advanced WPF features** still pending: person/crew search mode, streaming availability editing, richer relationship views
2. **Testing**: Add unit tests for services, integration tests for API endpoints, E2E tests for critical workflows
3. **Security**: Enable HTTPS, rotate credentials, implement proper secret management, upgrade to JWT/OAuth authentication
4. **Performance**: Consider caching/batching watch provider lookups, parallel TMDB requests for seasons
5. **User Experience**: Refine font sizing, improve mobile responsiveness
6. **Authentication**: Upgrade from header-based auth to JWT bearer tokens with proper validation and expiration

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

### Multi-User Authentication (v0.65)

**Architecture**: The multi-user system uses a **shared media catalog** with **per-user tracking data**:
- `Media` and `Episodes` tables store TMDB data (shared across all users)
- `UserMedia` and `UserEpisode` junction tables store user-specific data (watch states, priorities, notes)
- Each user sees the same media catalog but with their own personal tracking information

**Client-Side Implementation**:
1. **User Selection**: Login page (`Login.razor`) fetches users from `/api/users` and stores selected user in localStorage
2. **UserStateService**: Singleton service maintains current user state across the app
3. **AuthenticatedHttpMessageHandler**: Custom `DelegatingHandler` that automatically adds `X-User-Id` header to all outgoing HTTP requests
4. **HttpClient Setup**: Configured with the custom handler in `Program.cs` to inject user context into every API call

**Server-Side Implementation**:
1. **CurrentUserService**: Scoped service that extracts user ID from the `X-User-Id` request header
2. **IHttpContextAccessor**: Registered to provide access to the current HTTP context
3. **Dependency Injection**: All API endpoints inject `ICurrentUserService` to get the authenticated user ID
4. **Query Filtering**: Every database query filters by the current user's ID to ensure data isolation

**Critical Lessons Learned**:

1. **Query Logic Bug** (THE KEY ISSUE):
   - **Wrong**: `where includeHiddenValue || um == null || !um.Hidden`
   - This incorrectly showed ALL media to users with no UserMedia records (`um == null` matched everything)
   - **Correct**: `where um != null && (includeHiddenValue || !um.Hidden)`
   - Only show media that the user has explicitly added to their library

2. **Header Transmission**:
   - Use `DelegatingHandler` to automatically add headers to all requests
   - Don't manually add headers in each service method (error-prone and repetitive)
   - The handler wraps the `HttpClient` and intercepts all requests

3. **Service Scoping**:
   - `UserStateService` is **Singleton** (persists across app lifetime)
   - `CurrentUserService` is **Scoped** (per-request lifetime)
   - `AuthenticatedHttpMessageHandler` is **Scoped** (can inject scoped services)
   - `HttpClient` is **Scoped** (new instance per scope with the handler)

4. **User Context Extraction**:
   - Always use `ICurrentUserService.GetRequiredUserId()` in endpoints
   - Never hardcode user IDs (removed `const int TEMP_USER_ID = 101`)
   - Add comprehensive logging to debug header transmission issues

5. **Database Schema Design**:
   - Junction tables (`UserMedia`, `UserEpisode`) enable many-to-many relationships
   - Composite unique constraints (`UserId + MediaId`) prevent duplicate tracking
   - Cascade deletes ensure referential integrity
   - Separate shared data (Media/Episodes) from user data (UserMedia/UserEpisode)

6. **Testing Multi-User Isolation**:
   - Query the database directly to verify data distribution: `SELECT user_id, COUNT(*) FROM user_media GROUP BY user_id`
   - Test API endpoints with different `X-User-Id` headers: `curl -H "X-User-Id: 102" http://localhost:5000/api/media`
   - Check logs for "User ID from header" messages to confirm proper extraction
   - Verify SQL queries use `@__userId_0` parameter (not hardcoded values)

7. **Debugging Tips**:
   - Add debug logging in `CurrentUserService` to log all headers and user ID extraction
   - Add debug logging in endpoints to confirm which user ID is being used
   - Use `journalctl` to view API logs: `sudo journalctl -u myshowtime-api.service -n 50`
   - Check for EF Core SQL queries to see the actual parameter values

**Current Implementation Status**:
- ✅ Database schema supports full multi-user isolation
- ✅ User selection UI with login page
- ✅ Client-side user context passed via HTTP headers
- ✅ Server-side user context extraction and validation
- ✅ All API endpoints filter by current user
- ⚠️ Uses simple header-based auth (not production-ready)
- ❌ No JWT/OAuth token validation
- ❌ No session management or token expiration

**Next Steps for Production**:
1. Replace `X-User-Id` header with JWT bearer tokens
2. Add authentication middleware to validate tokens
3. Extract user claims from JWT instead of header
4. Implement token refresh and expiration
5. Add authorization policies for role-based access
6. Consider AWS Cognito integration (CognitoSub field already exists)

**Files to Reference**:
- See `DatabaseSchema.md` for complete database schema documentation
- Client: `src/MyShowtime.Client/Services/AuthenticatedHttpMessageHandler.cs`
- Client: `src/MyShowtime.Client/Services/UserStateService.cs`
- API: `src/MyShowtime.Api/Services/CurrentUserService.cs`
- API: `src/MyShowtime.Api/Program.cs` (all endpoint implementations)

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

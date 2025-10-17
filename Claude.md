# MyShowtime Development Log

## Current Status (October 17, 2025)
**Version 0.62 - Build 137**

Multi-user authentication system is **fully functional and working**. Moving forward with multiple user support.

## Recent Work: Multi-User Authentication Implementation

### Overview
Implemented a complete user authentication system to support multiple users accessing the application with their own data and preferences.

### Test Use Case (Verified Working)
1. **User goes to url/MyShowtime** → Ends up on the landing page (login page)
2. **User selects one of the users from a list** → Clicks a user card button
3. **User is directed to the app** → That user ID becomes their operational ID
4. **User can see their name in the top right** → Click "Logout" to end the logged in session → Sends them back to the landing page

### Implementation Details

#### Database Schema
- Created `app_user` table to store user profiles
- Added `user_id` columns to all user-specific data tables:
  - `user_media` (media library entries)
  - `user_episode` (episode watch states)
- Test users created: Alice Johnson (101), Bob Smith (102), Carol Williams (103)

#### Frontend Components
1. **UserStateService** (Singleton)
   - Manages user authentication state
   - Persists user selection to localStorage
   - Event-driven UI updates via `OnUserChanged` event

2. **Login Page** (`/login`)
   - Displays available users as clickable cards
   - Shows user initials in circular avatars
   - Styled with gradient background and hover effects
   - Uses EmptyLayout to avoid authentication checks

3. **MainLayout Updates**
   - Displays current user's name in header
   - Shows "Logout" button next to user name
   - Implements authentication check on first render
   - Redirects unauthenticated users to login page

4. **App.razor**
   - Simplified routing without complex authentication wrapper
   - Authentication handled in MainLayout for better timing

#### API Endpoints
- `GET /api/users` - Returns list of active users for login page

### Key Lessons Learned

1. **Service Lifetime Matters**
   - UserStateService must be **Singleton**, not Scoped
   - Scoped services create different instances per scope
   - Authentication state must be shared across the entire app

2. **Blazor Base Path Configuration**
   - Set `<base href="/MyShowtime/">` in index.html for subpath deployments
   - API calls must use **relative paths** (e.g., `api/users`) not absolute (`/api/users`)
   - Relative paths automatically resolve based on base href

3. **Event Timing in Blazor**
   - Components need to use `InvokeAsync(StateHasChanged)` for thread-safe UI updates
   - Authentication checks should happen in `OnAfterRender(firstRender)` not `OnInitialized`
   - This ensures UserStateService is fully initialized before checking auth state

4. **Component Lifecycle**
   - `OnInitialized` runs before the component is fully ready
   - `OnAfterRender(firstRender)` is the right place for post-initialization checks
   - Event subscriptions need proper cleanup in `Dispose()`

5. **Layout Isolation**
   - Created EmptyLayout for login page to avoid authentication redirects
   - MainLayout handles auth checks for all other pages
   - Prevents infinite redirect loops

### Technical Architecture

**Authentication Flow:**
```
User Access → MainLayout.OnAfterRender() → Check UserState.IsLoggedIn
  ↓ Not Logged In                              ↓ Logged In
Navigate to /login                         Render page + user info
  ↓
Login Page loads users from API
  ↓
User selects profile
  ↓
UserState.LoginAsync() → Save to localStorage → Fire OnUserChanged event
  ↓
Navigate to home → MainLayout re-renders → Shows user name & logout
```

**State Management:**
- UserStateService (Singleton) maintains authentication state
- localStorage provides persistence across sessions
- Event system notifies components of state changes

### Current Deployment
- **Build 137** deployed to `/var/www/projects/MyShowtime/`
- nginx serves static files from `wwwroot/`
- API proxied to localhost:5000
- Base path: `/MyShowtime/`

### Next Steps
- Continue building features with multi-user support
- All new data models should include `user_id` foreign keys
- User context flows through API using temp user ID (will be replaced with actual auth later)

## Previous Work

### Build 125 - TMDB Source Enrichment
- Added streaming source detection to TMDB search results
- Implemented concurrent API calls with semaphore throttling
- Added caching for watch provider lookups
- UI displays source badges on search cards

### Build 124 - Show Type Replacement
- Replaced legacy show types with proper media domain modeling
- Standardized on Movie and TvShow types
- Updated all database queries and DTOs

### Project Structure
```
MyShowtime/
├── src/
│   ├── MyShowtime.Api/         # ASP.NET Core backend
│   ├── MyShowtime.Client/      # Blazor WebAssembly frontend
│   └── MyShowtime.Shared/      # Shared DTOs and models
├── scripts/
│   └── build_and_deploy.sh     # Automated deployment
└── Claude.md                    # This file
```

### Tech Stack
- **Backend**: ASP.NET Core 8.0 Minimal API
- **Frontend**: Blazor WebAssembly
- **Database**: PostgreSQL
- **External API**: TMDB (The Movie Database)
- **Deployment**: nginx reverse proxy, systemd service

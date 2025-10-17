# MyShowtime Database Schema

## Overview
The database uses PostgreSQL and follows a multi-user architecture where media items (movies and TV shows) are shared across all users, but each user has their own personal tracking data (watch states, priorities, notes, etc.).

## Data Isolation Architecture

### Core Design
- **Media and Episodes** are shared across all users (single source of truth from TMDB)
- **User-specific data** is stored in junction tables (`user_media` and `user_episode`)
- Each user sees the same media catalog but with their own personal tracking data

## Tables

### 1. `app_user`
Stores user account information.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | integer | PRIMARY KEY, AUTO INCREMENT | User ID |
| `cognito_sub` | uuid | UNIQUE, NOT NULL | AWS Cognito subscriber ID |
| `email` | text | NOT NULL | User email address |
| `name` | text | NULL | Display name |
| `cognito_username` | text | NULL | Cognito username |
| `roles` | text[] | NULL | User roles array |
| `is_active` | boolean | DEFAULT true | Account active status |
| `metadata` | jsonb | NULL | Additional user metadata |
| `created_at` | timestamp with time zone | DEFAULT NOW() | Account creation timestamp |
| `updated_at` | timestamp with time zone | DEFAULT NOW() | Last update timestamp |
| `last_login` | timestamp with time zone | DEFAULT NOW() | Last login timestamp |

**Indexes:**
- PRIMARY KEY on `id`
- UNIQUE INDEX on `cognito_sub`

---

### 2. `Media`
Stores movies and TV shows from TMDB. Shared across all users.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | uuid | PRIMARY KEY | Media ID |
| `TmdbId` | integer | UNIQUE, NOT NULL | TMDB ID |
| `MediaType` | varchar(16) | NOT NULL | 'Movie' or 'TvShow' |
| `Title` | varchar(512) | NOT NULL | Media title |
| `Synopsis` | text | NULL | Media description |
| `ReleaseDate` | date | NULL | Release/first air date |
| `PosterPath` | varchar(256) | NULL | TMDB poster path |
| `Genres` | text | NULL | JSON array of genres |
| `Cast` | text | NULL | JSON array of cast members |
| `CreatedAtUtc` | timestamp with time zone | DEFAULT NOW() | Creation timestamp |
| `UpdatedAtUtc` | timestamp with time zone | NULL | Last update timestamp |
| `LastSyncedAtUtc` | timestamp with time zone | NULL | Last TMDB sync timestamp |

**Indexes:**
- PRIMARY KEY on `Id`
- UNIQUE INDEX on `TmdbId`

---

### 3. `Episodes`
Stores TV show episodes. Shared across all users.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | uuid | PRIMARY KEY | Episode ID |
| `MediaId` | uuid | FOREIGN KEY → Media | Parent TV show ID |
| `TmdbEpisodeId` | integer | NOT NULL | TMDB episode ID |
| `SeasonNumber` | integer | NOT NULL | Season number |
| `EpisodeNumber` | integer | NOT NULL | Episode number |
| `Title` | varchar(512) | NOT NULL | Episode title |
| `Synopsis` | text | NULL | Episode description |
| `AirDate` | date | NULL | Air date |
| `IsSpecial` | boolean | DEFAULT false | Special episode flag |
| `CreatedAtUtc` | timestamp with time zone | DEFAULT NOW() | Creation timestamp |
| `UpdatedAtUtc` | timestamp with time zone | NULL | Last update timestamp |

**Indexes:**
- PRIMARY KEY on `Id`
- UNIQUE INDEX on (`MediaId`, `SeasonNumber`, `EpisodeNumber`)

**Foreign Keys:**
- `MediaId` → `Media.Id` (CASCADE DELETE)

---

### 4. `user_media`
Junction table storing user-specific media tracking data.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | integer | PRIMARY KEY, AUTO INCREMENT | Record ID |
| `user_id` | integer | FOREIGN KEY → app_user | User ID |
| `media_id` | uuid | FOREIGN KEY → Media | Media ID |
| `watch_state` | varchar(16) | NOT NULL | 'Unwatched', 'Partial', or 'Watched' |
| `priority` | integer | DEFAULT 3 | Priority (0-10) |
| `hidden` | boolean | DEFAULT false | Hide from library |
| `notes` | text | NULL | User notes |
| `source` | text | NULL | Where to watch |
| `available_on` | text | NULL | Streaming service |
| `created_at` | timestamp with time zone | DEFAULT NOW() | Creation timestamp |
| `updated_at` | timestamp with time zone | DEFAULT NOW() | Last update timestamp |

**Indexes:**
- PRIMARY KEY on `id`
- UNIQUE INDEX on (`user_id`, `media_id`)
- INDEX on `media_id`

**Foreign Keys:**
- `user_id` → `app_user.id` (CASCADE DELETE)
- `media_id` → `Media.Id` (CASCADE DELETE)

---

### 5. `user_episode`
Junction table storing user-specific episode watch states.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | integer | PRIMARY KEY, AUTO INCREMENT | Record ID |
| `user_id` | integer | FOREIGN KEY → app_user | User ID |
| `episode_id` | uuid | FOREIGN KEY → Episodes | Episode ID |
| `watch_state` | varchar(16) | NOT NULL | 'Unwatched', 'Partial', or 'Watched' |
| `created_at` | timestamp with time zone | DEFAULT NOW() | Creation timestamp |
| `updated_at` | timestamp with time zone | DEFAULT NOW() | Last update timestamp |

**Indexes:**
- PRIMARY KEY on `id`
- UNIQUE INDEX on (`user_id`, `episode_id`)
- INDEX on `episode_id`

**Foreign Keys:**
- `user_id` → `app_user.id` (CASCADE DELETE)
- `episode_id` → `Episodes.Id` (CASCADE DELETE)

---

### 6. `user_settings`
Stores user preferences and settings.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | integer | PRIMARY KEY, AUTO INCREMENT | Record ID |
| `user_id` | integer | FOREIGN KEY → app_user, UNIQUE | User ID (one-to-one) |
| `last_selected_media_id` | uuid | FOREIGN KEY → Media | Last viewed media |
| `preferences` | jsonb | NULL | User preferences JSON |
| `created_at` | timestamp with time zone | DEFAULT NOW() | Creation timestamp |
| `updated_at` | timestamp with time zone | DEFAULT NOW() | Last update timestamp |

**Indexes:**
- PRIMARY KEY on `id`
- UNIQUE INDEX on `user_id`
- INDEX on `last_selected_media_id`

**Foreign Keys:**
- `user_id` → `app_user.id` (CASCADE DELETE)
- `last_selected_media_id` → `Media.Id` (SET NULL ON DELETE)

---

## Data Migration History

### Migration: `AddMultiUserArchitecture` (2024-10-17)
This migration:
1. Created the `user_media`, `user_episode`, and `user_settings` tables
2. Migrated all existing media data to user ID 101 (Alice Johnson)
3. Removed user-specific columns from the `Media` and `Episodes` tables

**Data Migration SQL:**
```sql
-- Migrate media tracking to user 101
INSERT INTO user_media (user_id, media_id, watch_state, priority, hidden, notes, source, available_on, created_at, updated_at)
SELECT 101 as user_id, "Id", COALESCE("WatchState", 'Unwatched'), COALESCE("Priority", 3), COALESCE("Hidden", false),
       "Notes", "Source", "AvailableOn", "CreatedAtUtc", COALESCE("UpdatedAtUtc", "CreatedAtUtc")
FROM "Media";

-- Migrate episode watch states to user 101
INSERT INTO user_episode (user_id, episode_id, watch_state, created_at, updated_at)
SELECT 101 as user_id, "Id", COALESCE("WatchState", 'Unwatched'), "CreatedAtUtc", COALESCE("UpdatedAtUtc", "CreatedAtUtc")
FROM "Episodes";
```

---

## Current Issue Investigation

### Expected Behavior
- User 101 (Alice) should see the migrated data
- User 102 (Bob) should see an empty library
- User 103 (Charlie) should see an empty library

### Potential Issues to Check

1. **User Creation**: Are users 102 and 103 actually in the database?
   ```sql
   SELECT id, name, email FROM app_user ORDER BY id;
   ```

2. **Data Distribution**: Check what data exists in user_media table:
   ```sql
   SELECT user_id, COUNT(*) as media_count
   FROM user_media
   GROUP BY user_id;
   ```

3. **Header Transmission**: Verify the X-User-Id header is being sent:
   - Check browser DevTools Network tab
   - Look for `X-User-Id` in request headers

4. **API Reception**: Verify the API is receiving and using the header:
   - Add logging to CurrentUserService
   - Check if GetRequiredUserId() is returning the correct value

5. **Query Filtering**: Ensure queries are using the dynamic user ID:
   - All instances of `um.UserId == userId` should use the injected user ID
   - No hardcoded user IDs should remain

---

## Notes

- All timestamps use `timestamp with time zone` (PostgreSQL best practice)
- JSON data is stored as `jsonb` for efficient querying
- Cascade deletes ensure referential integrity
- Unique constraints prevent duplicate user-media and user-episode relationships
- The schema supports complete data isolation between users while sharing the media catalog
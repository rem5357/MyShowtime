-- Data Migration Script for Multi-User Architecture
-- This script preserves existing single-user data by migrating it to User 101 (Alice Johnson)
-- Run this BEFORE applying the AddMultiUserArchitecture EF Core migration

-- Step 1: Insert test users with specific IDs
-- Note: User 101 will receive all existing data from the single-user setup

INSERT INTO app_user (id, cognito_sub, email, name, cognito_username, is_active, created_at, updated_at, last_login)
VALUES
    (101, 'a1111111-1111-1111-1111-111111111111'::uuid, 'alice@example.com', 'Alice Johnson', 'alice', true, NOW(), NOW(), NOW()),
    (102, 'b2222222-2222-2222-2222-222222222222'::uuid, 'bob@example.com', 'Bob Smith', 'bob', true, NOW(), NOW(), NOW()),
    (103, 'c3333333-3333-3333-3333-333333333333'::uuid, 'carol@example.com', 'Carol Williams', 'carol', true, NOW(), NOW(), NOW())
ON CONFLICT (id) DO NOTHING;

-- Reset the sequence to continue from 104
SELECT setval(pg_get_serial_sequence('app_user', 'id'), 103, true);

-- Step 2: Migrate existing Media data to user_media for User 101
-- This preserves Priority, WatchState, Hidden, Notes, Source, and AvailableOn fields

INSERT INTO user_media (user_id, media_id, watch_state, priority, hidden, notes, source, available_on, created_at, updated_at)
SELECT
    101 as user_id,
    "Id" as media_id,
    COALESCE("WatchState", 'Unwatched') as watch_state,
    COALESCE("Priority", 3) as priority,
    COALESCE("Hidden", false) as hidden,
    "Notes" as notes,
    "Source" as source,
    "AvailableOn" as available_on,
    "CreatedAtUtc" as created_at,
    COALESCE("UpdatedAtUtc", "CreatedAtUtc") as updated_at
FROM "Media"
WHERE "Id" IS NOT NULL;

-- Step 3: Migrate existing Episode watch states to user_episode for User 101
-- This preserves the WatchState field for each episode

INSERT INTO user_episode (user_id, episode_id, watch_state, created_at, updated_at)
SELECT
    101 as user_id,
    "Id" as episode_id,
    COALESCE("WatchState", 'Unwatched') as watch_state,
    "CreatedAtUtc" as created_at,
    COALESCE("UpdatedAtUtc", "CreatedAtUtc") as updated_at
FROM "Episodes"
WHERE "Id" IS NOT NULL;

-- Summary: Data migration completed
-- - Inserted 3 test users (101: Alice, 102: Bob, 103: Carol)
-- - Migrated all existing Media records to user_media for User 101
-- - Migrated all existing Episode records to user_episode for User 101
--
-- Next step: Apply the EF Core migration using:
-- dotnet ef database update

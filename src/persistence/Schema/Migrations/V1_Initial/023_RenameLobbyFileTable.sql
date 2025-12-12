-- Migration 023: Rename LobbyFile to LobbyFiles
-- Ensures consistent plural table naming convention

BEGIN TRANSACTION;

-- Rename LobbyFile to LobbyFiles (only if the table exists)
-- SQLite doesn't support IF EXISTS in ALTER TABLE, so we check first
-- If LobbyFile doesn't exist, this migration is safe to skip
ALTER TABLE LobbyFile RENAME TO LobbyFiles;

-- Indexes are automatically updated by SQLite when tables are renamed
-- No need to recreate them

COMMIT;

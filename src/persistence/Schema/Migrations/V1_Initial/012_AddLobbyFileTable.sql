-- Migration 012: Create LobbyFile table for Unit of Work pattern
-- Date: 2025-12-11
-- Description: Create normalized LobbyFile table with Unit of Work pattern
--              This replaces the old LobbyFiles table from migration 004

-- Create new LobbyFile table with simplified, normalized schema
CREATE TABLE IF NOT EXISTS LobbyFile (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RunNumber INTEGER NOT NULL,
    Sha256Hash TEXT NOT NULL,
    BinaryData BLOB,
    MatchIndex INTEGER,
    DetectedMapName TEXT,
    DetectedPlayer1 TEXT,
    DetectedPlayer2 TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT,
    UNIQUE(RunNumber, Sha256Hash)
);

-- Create indexes for common queries
CREATE INDEX IF NOT EXISTS idx_lobbyfile_runnumber ON LobbyFile(RunNumber);
CREATE INDEX IF NOT EXISTS idx_lobbyfile_hash ON LobbyFile(Sha256Hash);
CREATE INDEX IF NOT EXISTS idx_lobbyfile_matchindex ON LobbyFile(MatchIndex);

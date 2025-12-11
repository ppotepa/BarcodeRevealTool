-- Migration 007: Enhance DebugSession table for Unit of Work pattern
-- Date: 2025-12-11
-- Description: Update DebugSession to Unit of Work pattern with CreatedAt, UpdatedAt, ExitCode columns
--              Maps old DateStarted -> CreatedAt, DateCompleted -> UpdatedAt

-- Rename the old DebugSession table (created by migration 004)
ALTER TABLE DebugSession RENAME TO DebugSession_old;

-- Create new DebugSession table with Unit of Work pattern columns
CREATE TABLE IF NOT EXISTS DebugSession (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RunNumber INTEGER NOT NULL,
    ManualOpponentBattleTag TEXT,
    ManualOpponentNickname TEXT,
    PresetUserBattleTag TEXT,
    TotalMatchesPlayed INTEGER DEFAULT 0,
    TotalLobbiesProcessed INTEGER DEFAULT 0,
    IsCorrect BOOLEAN DEFAULT 1,
    Status TEXT NOT NULL DEFAULT 'InProgress',
    ExitCode INTEGER,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT,
    Notes TEXT
);

-- Copy data from old table, mapping DateStarted -> CreatedAt, DateCompleted -> UpdatedAt
INSERT INTO DebugSession (
    Id, RunNumber, ManualOpponentBattleTag, ManualOpponentNickname, 
    PresetUserBattleTag, TotalMatchesPlayed, TotalLobbiesProcessed, 
    IsCorrect, Status, CreatedAt, UpdatedAt, Notes
)
SELECT 
    Id, RunNumber, ManualOpponentBattleTag, ManualOpponentNickname,
    PresetUserBattleTag, TotalMatchesPlayed, TotalLobbiesProcessed,
    1, Status, DateStarted, DateCompleted, Notes
FROM DebugSession_old;

-- Drop old table
DROP TABLE DebugSession_old;

-- Create indexes
CREATE INDEX IF NOT EXISTS IDX_DebugSession_RunNumber ON DebugSession(RunNumber);
CREATE INDEX IF NOT EXISTS IDX_DebugSession_CreatedAt ON DebugSession(CreatedAt DESC);
CREATE INDEX IF NOT EXISTS IDX_DebugSession_Status ON DebugSession(Status);
CREATE INDEX IF NOT EXISTS IDX_DebugSession_IsCorrect ON DebugSession(IsCorrect DESC);


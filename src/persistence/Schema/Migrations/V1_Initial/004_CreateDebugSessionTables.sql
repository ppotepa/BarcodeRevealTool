-- Migration 004: Create DebugSession Table (Initial Version)
-- Date: 2025-12-11
-- Description: Create DebugSession table for debug mode session tracking
--              Uses old schema - will be migrated to Unit of Work pattern in migration 007

CREATE TABLE IF NOT EXISTS DebugSession (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RunNumber INTEGER NOT NULL,
    ManualOpponentBattleTag TEXT,
    ManualOpponentNickname TEXT,
    PresetUserBattleTag TEXT,
    TotalMatchesPlayed INTEGER DEFAULT 0,
    TotalLobbiesProcessed INTEGER DEFAULT 0,
    DateStarted DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DateCompleted DATETIME,
    Status TEXT NOT NULL DEFAULT 'InProgress',
    Notes TEXT
);

CREATE INDEX IF NOT EXISTS IDX_DebugSession_RunNumber ON DebugSession(RunNumber);
CREATE INDEX IF NOT EXISTS IDX_DebugSession_DateStarted ON DebugSession(DateStarted DESC);

-- Migration 001: Create RunInfo Table
-- Date: 2025-12-11
-- Description: Initial creation of RunInfo table for tracking application runs

CREATE TABLE IF NOT EXISTS RunInfo (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RunNumber INTEGER NOT NULL UNIQUE,
    DateStarted DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DateCompleted DATETIME,
    TotalReplaysProcessed INTEGER DEFAULT 0,
    Status TEXT NOT NULL DEFAULT 'InProgress',
    Mode TEXT NOT NULL,
    LogFileName TEXT,
    LogFilePath TEXT,
    Notes TEXT
);

CREATE INDEX IF NOT EXISTS IDX_RunInfo_RunNumber ON RunInfo(RunNumber DESC);
CREATE INDEX IF NOT EXISTS IDX_RunInfo_DateStarted ON RunInfo(DateStarted DESC);

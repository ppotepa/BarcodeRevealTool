-- Migration 016: Add BuildOrder table for Unit of Work pattern
-- Date: 2025-12-11
-- Description: Create BuildOrder table for build order records

CREATE TABLE IF NOT EXISTS BuildOrder (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    OpponentTag TEXT NOT NULL,
    OpponentNickname TEXT,
    TimeSeconds INTEGER,
    Kind TEXT,
    Name TEXT,
    ReplayFilePath TEXT,
    RecordedAt TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT
);

CREATE INDEX IF NOT EXISTS idx_buildorder_opponenttag ON BuildOrder(OpponentTag);
CREATE INDEX IF NOT EXISTS idx_buildorder_recordedat ON BuildOrder(RecordedAt);
CREATE INDEX IF NOT EXISTS idx_buildorder_timeseconds ON BuildOrder(TimeSeconds);

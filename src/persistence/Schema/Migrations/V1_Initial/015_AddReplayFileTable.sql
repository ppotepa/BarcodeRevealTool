-- Migration 015: Add ReplayFile table for Unit of Work pattern
-- Date: 2025-12-11
-- Description: Create ReplayFile table for replay records

CREATE TABLE IF NOT EXISTS ReplayFile (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    YourTag TEXT,
    OpponentTag TEXT,
    OpponentToon TEXT,
    OpponentNickname TEXT,
    Map TEXT,
    YourRace TEXT,
    OpponentRace TEXT,
    Result TEXT,
    GameDate TEXT,
    ReplayFilePath TEXT UNIQUE,
    Sc2ClientVersion TEXT,
    YouId TEXT,
    OpponentId TEXT,
    Winner TEXT,
    Note TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT
);

CREATE INDEX IF NOT EXISTS idx_replayfile_yourtag ON ReplayFile(YourTag);
CREATE INDEX IF NOT EXISTS idx_replayfile_opponenttag ON ReplayFile(OpponentTag);
CREATE INDEX IF NOT EXISTS idx_replayfile_gamedate ON ReplayFile(GameDate);
CREATE INDEX IF NOT EXISTS idx_replayfile_filepath ON ReplayFile(ReplayFilePath);
